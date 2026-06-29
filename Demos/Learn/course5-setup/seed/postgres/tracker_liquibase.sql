DROP TABLE IF EXISTS databasechangelog;
DROP TABLE IF EXISTS databasechangeloglock;
CREATE TABLE databasechangeloglock (
  id          INTEGER NOT NULL CONSTRAINT pk_databasechangeloglock PRIMARY KEY,
  locked      BOOLEAN NOT NULL,
  lockgranted TIMESTAMP NULL,
  lockedby    VARCHAR(255) NULL
);
INSERT INTO databasechangeloglock (id,locked) VALUES (1,false);
CREATE TABLE databasechangelog (
  id            VARCHAR(255) NOT NULL,
  author        VARCHAR(255) NOT NULL,
  filename      VARCHAR(255) NOT NULL,
  dateexecuted  TIMESTAMP NOT NULL,
  orderexecuted INTEGER NOT NULL,
  exectype      VARCHAR(10) NOT NULL,
  md5sum        VARCHAR(35),
  description   VARCHAR(255),
  comments      VARCHAR(255),
  tag           VARCHAR(255),
  liquibase     VARCHAR(20),
  contexts      VARCHAR(255),
  labels        VARCHAR(255),
  deployment_id VARCHAR(10)
);
INSERT INTO databasechangelog (id,author,filename,dateexecuted,orderexecuted,exectype,description)
VALUES ('1','dev','db.changelog-master.xml',now(),1,'EXECUTED','createTable shop'),
       ('2','dev','db.changelog-master.xml',now(),2,'EXECUTED','addForeignKeyConstraint');
