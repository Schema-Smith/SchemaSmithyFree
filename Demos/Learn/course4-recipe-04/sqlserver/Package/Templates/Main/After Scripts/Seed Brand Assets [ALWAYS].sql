-- The logo ships INSIDE the package as resources/logo.png. The BinaryFile token in Product.json
-- turns it into the platform's binary literal (0x... here), so this script seeds the image with no
-- external file handling at deploy time. The SeedCategories token (next statement) embeds the
-- reference-data file verbatim.
IF NOT EXISTS (SELECT 1 FROM dbo.BrandAssets WHERE AssetName = N'Default')
    INSERT INTO dbo.BrandAssets (AssetName, Image) VALUES (N'Default', {{DefaultLogo}});

{{SeedCategories}}
