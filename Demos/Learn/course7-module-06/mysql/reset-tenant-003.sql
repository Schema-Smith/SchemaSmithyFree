-- Undo the staged failure so UQ_Customer_Email can build on a re-run.
DELETE FROM Customer WHERE Email = 'dupe@shop.example';
