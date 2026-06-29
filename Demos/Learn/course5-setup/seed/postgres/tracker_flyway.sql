DROP TABLE IF EXISTS flyway_schema_history;
CREATE TABLE flyway_schema_history (
  installed_rank INTEGER NOT NULL CONSTRAINT pk_flyway_schema_history PRIMARY KEY,
  version        VARCHAR(50),
  description    VARCHAR(200) NOT NULL,
  type           VARCHAR(20)  NOT NULL,
  script         VARCHAR(1000) NOT NULL,
  checksum       INTEGER,
  installed_by   VARCHAR(100) NOT NULL,
  installed_on   TIMESTAMP NOT NULL DEFAULT now(),
  execution_time INTEGER NOT NULL,
  success        BOOLEAN NOT NULL
);
INSERT INTO flyway_schema_history (installed_rank,version,description,type,script,checksum,installed_by,execution_time,success)
VALUES (1,'1','create shop','SQL','V1__create_shop.sql',NULL,'flyway',12,true),
       (2,'2','add orderitem','SQL','V2__add_orderitem.sql',NULL,'flyway',8,true);
