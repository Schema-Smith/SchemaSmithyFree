DROP TABLE IF EXISTS flyway_schema_history;
CREATE TABLE flyway_schema_history (
  installed_rank INT NOT NULL,
  version        VARCHAR(50),
  description    VARCHAR(200) NOT NULL,
  type           VARCHAR(20)  NOT NULL,
  script         VARCHAR(1000) NOT NULL,
  checksum       INT,
  installed_by   VARCHAR(100) NOT NULL,
  installed_on   DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  execution_time INT NOT NULL,
  success        TINYINT(1) NOT NULL,
  CONSTRAINT PK_flyway_schema_history PRIMARY KEY (installed_rank)
) ENGINE=InnoDB;
INSERT INTO flyway_schema_history (installed_rank,version,description,type,script,checksum,installed_by,execution_time,success)
VALUES (1,'1','create shop','SQL','V1__create_shop.sql',NULL,'flyway',12,1),
       (2,'2','add orderitem','SQL','V2__add_orderitem.sql',NULL,'flyway',8,1);
