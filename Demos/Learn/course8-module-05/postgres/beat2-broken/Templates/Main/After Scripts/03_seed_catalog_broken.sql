-- Seed the product catalog -- but Sku is NULL and the column is NOT NULL, so this fails; recovered via mark-done.
INSERT INTO public.product (productid, name, sku, unitprice) VALUES (1, 'Anvil', NULL, 199.99);
