-- READ-ONLY reference. A hand-rolled migration pipeline — numbered SQL files run
-- in order by a shell script, with a home-grown schema_version table as the only
-- record of what ran. You do NOT run these; the course5-setup script already
-- applied their end state to shop_from_scripts. They're here so you can see the
-- "before" — including the inconsistencies that creep into hand-rolled pipelines.

-- 001: the first table. No existence guard — this one assumed a clean database.
CREATE TABLE dbo.Customer (
  CustomerId INT          NOT NULL CONSTRAINT PK_Customer PRIMARY KEY,
  Email      NVARCHAR(256) NOT NULL,
  FullName   NVARCHAR(200) NULL
);

INSERT INTO dbo.schema_version (version, description) VALUES (1, '001_create_customer.sql');
