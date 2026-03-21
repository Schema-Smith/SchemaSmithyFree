MERGE INTO Region AS target
USING (
    VALUES
        (1, 'Eastern'),
        (2, 'Western'),
        (3, 'Northern'),
        (4, 'Southern')
) AS source (RegionID, RegionDescription)
ON target.RegionID = source.RegionID
WHEN NOT MATCHED BY TARGET THEN
    INSERT (RegionID, RegionDescription)
    VALUES (source.RegionID, source.RegionDescription);