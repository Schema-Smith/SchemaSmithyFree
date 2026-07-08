INSERT INTO product_status (code, description)
VALUES
    ('ACTIVE', 'Active'),
    ('DISCONTINUED', 'Discontinued'),
    ('DRAFT', 'Draft')
ON CONFLICT (code) DO NOTHING;
