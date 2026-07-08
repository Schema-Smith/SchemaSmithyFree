-- fleet_tenant_004 drift (out-of-band): someone dropped fk_orderitem_product and left an
-- orphan orderitem (productid 999 has no parent product). The rollout doesn't touch this
-- FK -- but the convergence engine re-checks the whole model, finds the FK "missing,"
-- recreates it, and the orphan fails it (PG 23503) at the foreign-keys phase.
-- salesorder/customer parents are valid, so ONLY the product FK fails -- deterministic.
-- Run against the fleet_tenant_004 database: psql ... -d fleet_tenant_004
DELETE FROM public.orderitem; DELETE FROM public.salesorder; DELETE FROM public.customer;
INSERT INTO public.customer (customerid, email, fullname) VALUES (1, 'c1@shop.example', 'Carl Index');
INSERT INTO public.salesorder (orderid, customerid, orderdate, status) VALUES (1, 1, now(), 'OPEN');
ALTER TABLE public.orderitem DROP CONSTRAINT IF EXISTS fk_orderitem_product;
INSERT INTO public.orderitem (orderitemid, orderid, productid, quantity, unitprice) VALUES (1, 1, 999, 1, 1.00);
