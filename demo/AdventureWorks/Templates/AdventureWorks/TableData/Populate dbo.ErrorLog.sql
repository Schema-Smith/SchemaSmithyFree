
DECLARE @v_json NVARCHAR(MAX) = '';

ALTER TABLE [dbo].[ErrorLog] DISABLE TRIGGER ALL;
SET IDENTITY_INSERT [dbo].[ErrorLog] ON; 
MERGE INTO [dbo].[ErrorLog] AS Target
USING (
  SELECT [ErrorLine],[ErrorLogID],[ErrorMessage],[ErrorNumber],[ErrorProcedure],[ErrorSeverity],[ErrorState],[ErrorTime],[UserName]
    FROM OPENJSON(@v_json)
    WITH (
           [ErrorLine] INT,
           [ErrorLogID] INT,
           [ErrorMessage] NVARCHAR(4000),
           [ErrorNumber] INT,
           [ErrorProcedure] NVARCHAR(126),
           [ErrorSeverity] INT,
           [ErrorState] INT,
           [ErrorTime] DATETIME,
           [UserName] SYSNAME
    )
) AS Source
ON Source.[ErrorLogID] = Target.[ErrorLogID]


WHEN MATCHED AND (NOT (Target.[ErrorLine] = Source.[ErrorLine] OR (Target.[ErrorLine] IS NULL AND Source.[ErrorLine] IS NULL)) AND NOT (Target.[ErrorMessage] = Source.[ErrorMessage] OR (Target.[ErrorMessage] IS NULL AND Source.[ErrorMessage] IS NULL)) AND NOT (Target.[ErrorNumber] = Source.[ErrorNumber] OR (Target.[ErrorNumber] IS NULL AND Source.[ErrorNumber] IS NULL)) AND NOT (Target.[ErrorProcedure] = Source.[ErrorProcedure] OR (Target.[ErrorProcedure] IS NULL AND Source.[ErrorProcedure] IS NULL)) AND NOT (Target.[ErrorSeverity] = Source.[ErrorSeverity] OR (Target.[ErrorSeverity] IS NULL AND Source.[ErrorSeverity] IS NULL)) AND NOT (Target.[ErrorState] = Source.[ErrorState] OR (Target.[ErrorState] IS NULL AND Source.[ErrorState] IS NULL)) AND NOT (Target.[ErrorTime] = Source.[ErrorTime] OR (Target.[ErrorTime] IS NULL AND Source.[ErrorTime] IS NULL)) AND NOT (Target.[UserName] = Source.[UserName] OR (Target.[UserName] IS NULL AND Source.[UserName] IS NULL))) THEN
  UPDATE SET
        [ErrorLine] = Source.[ErrorLine],
        [ErrorMessage] = Source.[ErrorMessage],
        [ErrorNumber] = Source.[ErrorNumber],
        [ErrorProcedure] = Source.[ErrorProcedure],
        [ErrorSeverity] = Source.[ErrorSeverity],
        [ErrorState] = Source.[ErrorState],
        [ErrorTime] = Source.[ErrorTime],
        [UserName] = Source.[UserName]


WHEN NOT MATCHED THEN
  INSERT (
        [ErrorLine],
        [ErrorLogID],
        [ErrorMessage],
        [ErrorNumber],
        [ErrorProcedure],
        [ErrorSeverity],
        [ErrorState],
        [ErrorTime],
        [UserName]
  ) VALUES (
        Source.[ErrorLine],
        Source.[ErrorLogID],
        Source.[ErrorMessage],
        Source.[ErrorNumber],
        Source.[ErrorProcedure],
        Source.[ErrorSeverity],
        Source.[ErrorState],
        Source.[ErrorTime],
        Source.[UserName]  
  )
;
SET IDENTITY_INSERT [dbo].[ErrorLog] OFF;
ALTER TABLE [dbo].[ErrorLog] ENABLE TRIGGER ALL;
