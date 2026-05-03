-- Copyright (c) SchemaSmith, LLC. All rights reserved.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

CREATE TABLE "SchemaSmith"."TestLog" ("Id" INT GENERATED ALWAYS AS IDENTITY NOT NULL, "Msg" VARCHAR(2000) NOT NULL);