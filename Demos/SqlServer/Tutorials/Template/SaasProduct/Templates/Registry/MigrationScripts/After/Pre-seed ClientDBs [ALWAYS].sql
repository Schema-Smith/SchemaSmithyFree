MERGE INTO Registry.dbo.ClientDBs AS Target
USING (
    VALUES
        ('First client',  'Client1'),
        ('Second client', 'Client2')
) AS Source (ClientName, DatabaseName)
  ON Target.DatabaseName = Source.DatabaseName

-- When a row exists, update its ClientName
WHEN MATCHED THEN
    UPDATE
    SET ClientName = Source.ClientName

-- When no matching row, insert a new one
WHEN NOT MATCHED BY TARGET THEN
    INSERT (ClientName, DatabaseName)
    VALUES (Source.ClientName, Source.DatabaseName)
;
