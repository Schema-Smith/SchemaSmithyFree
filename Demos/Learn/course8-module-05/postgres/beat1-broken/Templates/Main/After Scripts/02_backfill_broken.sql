-- Broken backfill: intentionally passes NULL for the required email column; this script will fail.
INSERT INTO public.customer (customerid, email, fullname) VALUES (21, NULL, 'Ivan Bronze');
