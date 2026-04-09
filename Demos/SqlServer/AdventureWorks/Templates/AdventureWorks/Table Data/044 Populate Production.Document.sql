
DECLARE @v_json NVARCHAR(MAX) = '{{Production.Document.tabledata}}';



MERGE INTO [Production].[Document] AS Target
USING (
  SELECT [ChangeNumber],[Document],[DocumentNode],[DocumentSummary],[FileExtension],[FileName],[FolderFlag],[ModifiedDate],[Owner],[Revision],[Status],[Title]
    FROM OPENJSON(@v_json)
    WITH (
           [ChangeNumber] INT,
           [Document] VARBINARY(MAX),
           [DocumentNode] NVARCHAR(4000),
           [DocumentSummary] NVARCHAR(MAX),
           [FileExtension] NVARCHAR(8),
           [FileName] NVARCHAR(400),
           [FolderFlag] BIT,
           [ModifiedDate] DATETIME,
           [Owner] INT,
           [Revision] NCHAR(5),
           [rowguid] UNIQUEIDENTIFIER,
           [Status] TINYINT,
           [Title] NVARCHAR(50)
    )
) AS Source
ON Source.[DocumentNode] = Target.[DocumentNode]

WHEN MATCHED AND (NOT (Target.[ChangeNumber] = Source.[ChangeNumber] OR (Target.[ChangeNumber] IS NULL AND Source.[ChangeNumber] IS NULL)) OR NOT (Target.[Document] = Source.[Document] OR (Target.[Document] IS NULL AND Source.[Document] IS NULL)) OR NOT (Target.[DocumentNode] = Source.[DocumentNode] OR (Target.[DocumentNode] IS NULL AND Source.[DocumentNode] IS NULL)) OR NOT (Target.[DocumentSummary] = Source.[DocumentSummary] OR (Target.[DocumentSummary] IS NULL AND Source.[DocumentSummary] IS NULL)) OR NOT (Target.[FileExtension] = Source.[FileExtension] OR (Target.[FileExtension] IS NULL AND Source.[FileExtension] IS NULL)) OR NOT (Target.[FileName] = Source.[FileName] OR (Target.[FileName] IS NULL AND Source.[FileName] IS NULL)) OR NOT (Target.[FolderFlag] = Source.[FolderFlag] OR (Target.[FolderFlag] IS NULL AND Source.[FolderFlag] IS NULL)) OR NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) OR NOT (Target.[Owner] = Source.[Owner] OR (Target.[Owner] IS NULL AND Source.[Owner] IS NULL)) OR NOT (Target.[Revision] = Source.[Revision] OR (Target.[Revision] IS NULL AND Source.[Revision] IS NULL)) OR NOT (Target.[Status] = Source.[Status] OR (Target.[Status] IS NULL AND Source.[Status] IS NULL)) OR NOT (Target.[Title] = Source.[Title] OR (Target.[Title] IS NULL AND Source.[Title] IS NULL))) THEN
  UPDATE SET
        [ChangeNumber] = Source.[ChangeNumber],
        [Document] = Source.[Document],
        [DocumentNode] = Source.[DocumentNode],
        [DocumentSummary] = Source.[DocumentSummary],
        [FileExtension] = Source.[FileExtension],
        [FileName] = Source.[FileName],
        [FolderFlag] = Source.[FolderFlag],
        [ModifiedDate] = Source.[ModifiedDate],
        [Owner] = Source.[Owner],
        [Revision] = Source.[Revision],
        [Status] = Source.[Status],
        [Title] = Source.[Title]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [ChangeNumber],
        [Document],
        [DocumentNode],
        [DocumentSummary],
        [FileExtension],
        [FileName],
        [FolderFlag],
        [ModifiedDate],
        [Owner],
        [Revision],
        [Status],
        [Title]
   ) VALUES (
         Source.[ChangeNumber],
        Source.[Document],
        Source.[DocumentNode],
        Source.[DocumentSummary],
        Source.[FileExtension],
        Source.[FileName],
        Source.[FolderFlag],
        Source.[ModifiedDate],
        Source.[Owner],
        Source.[Revision],
        Source.[Status],
        Source.[Title]
   )
 ;
