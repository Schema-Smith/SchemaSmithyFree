-- ============================================================================
-- Recyclebin Cleanup Job
-- Runs nightly at 11:00 PM against {{NorthwindDb}} executing
-- [recyclebin].[CleanupJob] to purge expired recyclebin tables.
--
-- Idempotent: safe to run on every Quench deployment.
-- Edit the variables below to change job behavior on the next deployment.
-- ============================================================================

-- ======================= CONFIGURABLE SETTINGS =============================
DECLARE @JobName            SYSNAME        = N'{{NorthwindDb}} - Recyclebin Cleanup';
DECLARE @JobEnabled         TINYINT        = 1;
DECLARE @JobDescription     NVARCHAR(512)  = N'Nightly cleanup of expired recyclebin tables.';
DECLARE @JobCategory        SYSNAME        = N'Database Maintenance';
DECLARE @JobOwnerLogin      SYSNAME        = N'TestUser';
DECLARE @NotifyLevel        INT            = 2;   -- 0=Never, 1=On success, 2=On failure, 3=Always

DECLARE @StepName           SYSNAME        = N'Execute CleanupJob';
DECLARE @Database           SYSNAME        = N'{{NorthwindDb}}';
DECLARE @Command            NVARCHAR(MAX)  = N'EXEC [recyclebin].[CleanupJob];';
DECLARE @RetryAttempts      INT            = 0;
DECLARE @RetryInterval      INT            = 0;   -- minutes

DECLARE @SchedName          SYSNAME        = N'Nightly at 11 PM';
DECLARE @SchedEnabled       INT            = 1;
DECLARE @SchedFreqType      INT            = 4;        -- 4=Daily
DECLARE @SchedFreqInterval  INT            = 1;        -- Every 1 day
DECLARE @SchedSubdayType    INT            = 1;        -- 1=At the specified time
DECLARE @SchedStartTime     INT            = 230000;   -- 23:00:00 (11 PM)
-- ===========================================================================

DECLARE @JobId UNIQUEIDENTIFIER;

-- ============================================================================
-- 1. Create the job if it does not exist
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM msdb.dbo.sysjobs WHERE name = @JobName)
BEGIN
    EXEC msdb.dbo.sp_add_job
        @job_name              = @JobName,
        @enabled               = @JobEnabled,
        @description           = @JobDescription,
        @category_name         = @JobCategory,
        @owner_login_name      = @JobOwnerLogin,
        @notify_level_eventlog = @NotifyLevel;

    EXEC msdb.dbo.sp_add_jobserver
        @job_name    = @JobName,
        @server_name = N'(LOCAL)';

    PRINT N'Created SQL Agent job: ' + @JobName;
END

SELECT @JobId = job_id FROM msdb.dbo.sysjobs WHERE name = @JobName;

-- ============================================================================
-- 2. Update job properties if they have drifted
-- ============================================================================
IF NOT EXISTS (
    SELECT 1
    FROM   msdb.dbo.sysjobs       j
    JOIN   msdb.dbo.syscategories  c ON j.category_id = c.category_id
    JOIN   sys.server_principals   p ON j.owner_sid   = p.sid
    WHERE  j.job_id                = @JobId
      AND  j.enabled               = @JobEnabled
      AND  j.description           = @JobDescription
      AND  c.name                  = @JobCategory
      AND  p.name                  = @JobOwnerLogin
      AND  j.notify_level_eventlog = @NotifyLevel
)
BEGIN
    EXEC msdb.dbo.sp_update_job
        @job_id                = @JobId,
        @enabled               = @JobEnabled,
        @description           = @JobDescription,
        @category_name         = @JobCategory,
        @owner_login_name      = @JobOwnerLogin,
        @notify_level_eventlog = @NotifyLevel;

    PRINT N'Updated job properties: ' + @JobName;
END

-- ============================================================================
-- 3. Upsert the job step
-- ============================================================================
IF NOT EXISTS (
    SELECT 1
    FROM   msdb.dbo.sysjobsteps
    WHERE  job_id    = @JobId
      AND  step_name = @StepName
)
BEGIN
    EXEC msdb.dbo.sp_add_jobstep
        @job_id            = @JobId,
        @step_name         = @StepName,
        @step_id           = 1,
        @subsystem         = N'TSQL',
        @command           = @Command,
        @database_name     = @Database,
        @on_success_action = 1,   -- Quit with success
        @on_fail_action    = 2,   -- Quit with failure
        @retry_attempts    = @RetryAttempts,
        @retry_interval    = @RetryInterval;

    PRINT N'Added job step: ' + @StepName;
END
ELSE IF NOT EXISTS (
    SELECT 1
    FROM   msdb.dbo.sysjobsteps
    WHERE  job_id          = @JobId
      AND  step_name       = @StepName
      AND  command         = @Command
      AND  database_name   = @Database
      AND  retry_attempts  = @RetryAttempts
      AND  retry_interval  = @RetryInterval
)
BEGIN
    EXEC msdb.dbo.sp_update_jobstep
        @job_id         = @JobId,
        @step_id        = 1,
        @command        = @Command,
        @database_name  = @Database,
        @retry_attempts = @RetryAttempts,
        @retry_interval = @RetryInterval;

    PRINT N'Updated job step: ' + @StepName;
END

-- ============================================================================
-- 4. Upsert the schedule
-- ============================================================================
IF NOT EXISTS (
    SELECT 1
    FROM   msdb.dbo.sysjobschedules js
    JOIN   msdb.dbo.sysschedules    s ON js.schedule_id = s.schedule_id
    WHERE  js.job_id = @JobId
      AND  s.name    = @SchedName
)
BEGIN
    EXEC msdb.dbo.sp_add_jobschedule
        @job_id            = @JobId,
        @name              = @SchedName,
        @enabled           = @SchedEnabled,
        @freq_type         = @SchedFreqType,
        @freq_interval     = @SchedFreqInterval,
        @freq_subday_type  = @SchedSubdayType,
        @active_start_time = @SchedStartTime;

    PRINT N'Added schedule: ' + @SchedName;
END
ELSE
BEGIN
    DECLARE @ScheduleId INT;
    SELECT @ScheduleId = s.schedule_id
    FROM   msdb.dbo.sysjobschedules js
    JOIN   msdb.dbo.sysschedules    s ON js.schedule_id = s.schedule_id
    WHERE  js.job_id = @JobId
      AND  s.name    = @SchedName;

    IF NOT EXISTS (
        SELECT 1
        FROM   msdb.dbo.sysschedules
        WHERE  schedule_id       = @ScheduleId
          AND  enabled           = @SchedEnabled
          AND  freq_type         = @SchedFreqType
          AND  freq_interval     = @SchedFreqInterval
          AND  freq_subday_type  = @SchedSubdayType
          AND  active_start_time = @SchedStartTime
    )
    BEGIN
        EXEC msdb.dbo.sp_update_schedule
            @schedule_id       = @ScheduleId,
            @enabled           = @SchedEnabled,
            @freq_type         = @SchedFreqType,
            @freq_interval     = @SchedFreqInterval,
            @freq_subday_type  = @SchedSubdayType,
            @active_start_time = @SchedStartTime;

        PRINT N'Updated schedule: ' + @SchedName;
    END
END

PRINT N'Job deployment complete: ' + @JobName;
GO
