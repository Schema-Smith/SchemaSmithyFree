-- Module 4 seed (PostgreSQL): a small "existing" music-catalog database that
-- nobody has under source control yet. Plain DDL + a little data — NOT a
-- SchemaSmith package. You'll point SchemaTongs at it and cast it into one.
-- A trimmed-down slice of the classic Chinook sample schema.
-- Run connected to any existing database (e.g. learn); the \c switches into chinook.

CREATE DATABASE chinook;
\c chinook

CREATE TABLE artist (
    artist_id INT GENERATED ALWAYS AS IDENTITY CONSTRAINT pk_artist PRIMARY KEY,
    name VARCHAR(120)
);

CREATE TABLE genre (
    genre_id INT GENERATED ALWAYS AS IDENTITY CONSTRAINT pk_genre PRIMARY KEY,
    name VARCHAR(120)
);

CREATE TABLE media_type (
    media_type_id INT GENERATED ALWAYS AS IDENTITY CONSTRAINT pk_media_type PRIMARY KEY,
    name VARCHAR(120)
);

CREATE TABLE album (
    album_id INT GENERATED ALWAYS AS IDENTITY CONSTRAINT pk_album PRIMARY KEY,
    title VARCHAR(160) NOT NULL,
    artist_id INT NOT NULL CONSTRAINT fk_album_artist REFERENCES artist(artist_id)
);
CREATE INDEX ix_album_artist_id ON album(artist_id);

CREATE TABLE track (
    track_id INT GENERATED ALWAYS AS IDENTITY CONSTRAINT pk_track PRIMARY KEY,
    name VARCHAR(200) NOT NULL,
    album_id INT CONSTRAINT fk_track_album REFERENCES album(album_id),
    media_type_id INT NOT NULL CONSTRAINT fk_track_media_type REFERENCES media_type(media_type_id),
    genre_id INT CONSTRAINT fk_track_genre REFERENCES genre(genre_id),
    composer VARCHAR(220),
    milliseconds INT NOT NULL,
    unit_price NUMERIC(10,2) NOT NULL
);
CREATE INDEX ix_track_album_id ON track(album_id);

CREATE TABLE playlist (
    playlist_id INT GENERATED ALWAYS AS IDENTITY CONSTRAINT pk_playlist PRIMARY KEY,
    name VARCHAR(120)
);

CREATE TABLE playlist_track (
    playlist_id INT NOT NULL CONSTRAINT fk_playlist_track_playlist REFERENCES playlist(playlist_id),
    track_id INT NOT NULL CONSTRAINT fk_playlist_track_track REFERENCES track(track_id),
    CONSTRAINT pk_playlist_track PRIMARY KEY (playlist_id, track_id)
);

INSERT INTO genre (name) VALUES ('Rock'), ('Jazz'), ('Metal');
INSERT INTO media_type (name) VALUES ('MPEG audio file'), ('AAC audio file');
INSERT INTO artist (name) VALUES ('AC/DC'), ('Accept'), ('Aerosmith'), ('Miles Davis'), ('Metallica');
INSERT INTO album (title, artist_id) VALUES
    ('For Those About To Rock We Salute You', 1),
    ('Balls to the Wall', 2),
    ('Big Ones', 3),
    ('Kind of Blue', 4),
    ('Master of Puppets', 5);
INSERT INTO track (name, album_id, media_type_id, genre_id, composer, milliseconds, unit_price) VALUES
    ('For Those About To Rock (We Salute You)', 1, 1, 1, 'Young, Young, Johnson', 343719, 0.99),
    ('Balls to the Wall', 2, 1, 3, NULL, 342562, 0.99),
    ('So What', 4, 1, 2, 'Miles Davis', 562000, 0.99),
    ('Master of Puppets', 5, 1, 3, 'Hetfield, Ulrich, Burton, Hammett', 515000, 0.99);
INSERT INTO playlist (name) VALUES ('My favorites');
INSERT INTO playlist_track (playlist_id, track_id) VALUES (1, 1), (1, 4);
