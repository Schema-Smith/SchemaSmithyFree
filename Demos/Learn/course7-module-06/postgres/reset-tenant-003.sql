-- Undo the staged failure so uq_customer_email can build on a re-run.
DELETE FROM customer WHERE email = 'dupe@shop.example';
