IF NOT EXISTS (SELECT 1 FROM dbo.OrderStatus WHERE [Code] = 'NEW')
    INSERT dbo.OrderStatus ([Code], [Description]) VALUES ('NEW', 'New order');
IF NOT EXISTS (SELECT 1 FROM dbo.OrderStatus WHERE [Code] = 'PAID')
    INSERT dbo.OrderStatus ([Code], [Description]) VALUES ('PAID', 'Paid');
IF NOT EXISTS (SELECT 1 FROM dbo.OrderStatus WHERE [Code] = 'SHIPPED')
    INSERT dbo.OrderStatus ([Code], [Description]) VALUES ('SHIPPED', 'Shipped');
IF NOT EXISTS (SELECT 1 FROM dbo.OrderStatus WHERE [Code] = 'CANCELLED')
    INSERT dbo.OrderStatus ([Code], [Description]) VALUES ('CANCELLED', 'Cancelled');
