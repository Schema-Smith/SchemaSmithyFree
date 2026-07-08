-- Fix the drift: collapse the duplicate email so the unique index builds on resume.
USE [fleet_tenant_002];
UPDATE dbo.Customer SET Email = 'grace@shop.example' WHERE CustomerId = 2;
