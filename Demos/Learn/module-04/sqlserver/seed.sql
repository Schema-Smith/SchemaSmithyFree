-- Module 4 seed (SQL Server): a small "existing" music-catalog database that
-- nobody has under source control yet. Plain DDL + a little data — NOT a
-- SchemaSmith package. You'll point SchemaTongs at it and cast it into one.
-- A trimmed-down slice of the classic Chinook sample schema.

IF DB_ID('chinook') IS NULL CREATE DATABASE chinook;
GO
USE chinook;
GO

CREATE TABLE dbo.Artist (
    ArtistId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Artist PRIMARY KEY,
    Name NVARCHAR(120) NULL
);

CREATE TABLE dbo.Genre (
    GenreId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Genre PRIMARY KEY,
    Name NVARCHAR(120) NULL
);

CREATE TABLE dbo.MediaType (
    MediaTypeId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_MediaType PRIMARY KEY,
    Name NVARCHAR(120) NULL
);

CREATE TABLE dbo.Album (
    AlbumId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Album PRIMARY KEY,
    Title NVARCHAR(160) NOT NULL,
    ArtistId INT NOT NULL CONSTRAINT FK_Album_Artist FOREIGN KEY REFERENCES dbo.Artist(ArtistId)
);
CREATE INDEX IX_Album_ArtistId ON dbo.Album(ArtistId);

CREATE TABLE dbo.Track (
    TrackId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Track PRIMARY KEY,
    Name NVARCHAR(200) NOT NULL,
    AlbumId INT NULL CONSTRAINT FK_Track_Album FOREIGN KEY REFERENCES dbo.Album(AlbumId),
    MediaTypeId INT NOT NULL CONSTRAINT FK_Track_MediaType FOREIGN KEY REFERENCES dbo.MediaType(MediaTypeId),
    GenreId INT NULL CONSTRAINT FK_Track_Genre FOREIGN KEY REFERENCES dbo.Genre(GenreId),
    Composer NVARCHAR(220) NULL,
    Milliseconds INT NOT NULL,
    UnitPrice DECIMAL(10,2) NOT NULL
);
CREATE INDEX IX_Track_AlbumId ON dbo.Track(AlbumId);

CREATE TABLE dbo.Playlist (
    PlaylistId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Playlist PRIMARY KEY,
    Name NVARCHAR(120) NULL
);

CREATE TABLE dbo.PlaylistTrack (
    PlaylistId INT NOT NULL CONSTRAINT FK_PlaylistTrack_Playlist FOREIGN KEY REFERENCES dbo.Playlist(PlaylistId),
    TrackId INT NOT NULL CONSTRAINT FK_PlaylistTrack_Track FOREIGN KEY REFERENCES dbo.Track(TrackId),
    CONSTRAINT PK_PlaylistTrack PRIMARY KEY (PlaylistId, TrackId)
);
GO

INSERT INTO dbo.Genre (Name) VALUES ('Rock'), ('Jazz'), ('Metal');
INSERT INTO dbo.MediaType (Name) VALUES ('MPEG audio file'), ('AAC audio file');
INSERT INTO dbo.Artist (Name) VALUES ('AC/DC'), ('Accept'), ('Aerosmith'), ('Miles Davis'), ('Metallica');
INSERT INTO dbo.Album (Title, ArtistId) VALUES
    ('For Those About To Rock We Salute You', 1),
    ('Balls to the Wall', 2),
    ('Big Ones', 3),
    ('Kind of Blue', 4),
    ('Master of Puppets', 5);
INSERT INTO dbo.Track (Name, AlbumId, MediaTypeId, GenreId, Composer, Milliseconds, UnitPrice) VALUES
    ('For Those About To Rock (We Salute You)', 1, 1, 1, 'Young, Young, Johnson', 343719, 0.99),
    ('Balls to the Wall', 2, 1, 3, NULL, 342562, 0.99),
    ('So What', 4, 1, 2, 'Miles Davis', 562000, 0.99),
    ('Master of Puppets', 5, 1, 3, 'Hetfield, Ulrich, Burton, Hammett', 515000, 0.99);
INSERT INTO dbo.Playlist (Name) VALUES ('My favorites');
INSERT INTO dbo.PlaylistTrack (PlaylistId, TrackId) VALUES (1, 1), (1, 4);
GO
