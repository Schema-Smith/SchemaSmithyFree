-- fleet_tenant_002 drift: two customers share an email. When the fleet rolls out the
-- new uq_customer_email unique index, this tenant's index build fails (PG 23505) at
-- the indexes-and-constraints phase, while the rest of the fleet hardens it fine.
-- Run against the fleet_tenant_002 database: psql ... -d fleet_tenant_002
DELETE FROM public.orderitem; DELETE FROM public.salesorder; DELETE FROM public.customer;
INSERT INTO public.customer (customerid, email, fullname) VALUES
  (1, 'dupe@shop.example', 'Ada Byte'),
  (2, 'dupe@shop.example', 'Grace Stack');
