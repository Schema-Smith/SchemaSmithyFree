DECLARE @data VARCHAR(MAX) = '[
  {
    "ShiftID": 1,
    "Name": "Day",
    "StartTime": "07:00:00",
    "EndTime": "15:00:00",
    "ModifiedDate": "2008-04-30T00:00:00"
  },
  {
    "ShiftID": 2,
    "Name": "Evening",
    "StartTime": "15:00:00",
    "EndTime": "23:00:00",
    "ModifiedDate": "2008-04-30T00:00:00"
  },
  {
    "ShiftID": 3,
    "Name": "Night",
    "StartTime": "23:00:00",
    "EndTime": "07:00:00",
    "ModifiedDate": "2008-04-30T00:00:00"
  }
]'

MERGE INTO HumanResources.Shift AS Target
USING (
    SELECT *
    FROM OPENJSON(@data)
    WITH (
        ShiftID int,
        Name nvarchar(50),
        StartTime time(7),
        EndTime time(7),
        ModifiedDate datetime
    )
) AS Source
ON Target.ShiftID = Source.ShiftID
WHEN MATCHED THEN
    UPDATE SET
        Name = Source.Name,
        StartTime = Source.StartTime,
        EndTime = Source.EndTime,
        ModifiedDate = Source.ModifiedDate
WHEN NOT MATCHED THEN
    INSERT (Name, StartTime, EndTime, ModifiedDate)
    VALUES (Source.Name, Source.StartTime, Source.EndTime, Source.ModifiedDate);

