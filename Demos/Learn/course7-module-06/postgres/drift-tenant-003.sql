-- Stage the failure on ONE tenant: duplicate emails block the new uq_customer_email unique index.
INSERT INTO customer (customerid, email, fullname)
VALUES (1, 'dupe@shop.example', 'Ada Lovelace'),
       (2, 'dupe@shop.example', 'Alan Turing');
