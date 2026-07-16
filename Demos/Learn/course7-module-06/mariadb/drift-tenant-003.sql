-- Stage the failure on ONE tenant: duplicate emails block the new UQ_Customer_Email unique index.
INSERT INTO Customer (CustomerId, Email, FullName)
VALUES (1, 'dupe@shop.example', 'Ada Lovelace'),
       (2, 'dupe@shop.example', 'Alan Turing');
