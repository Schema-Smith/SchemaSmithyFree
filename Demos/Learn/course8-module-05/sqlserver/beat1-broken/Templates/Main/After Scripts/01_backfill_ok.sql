-- Valid backfill: inserts a new customer row with all required fields populated.
INSERT dbo.Customer ([CustomerId],[Email],[FullName]) VALUES (20, 'hana.z@shop.test', 'Hana Zhou');
