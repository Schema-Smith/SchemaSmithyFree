-- The logo ships INSIDE the package as resources/logo.png. The BinaryFile token in Product.json
-- turns it into the platform's binary literal (E'\x...'::bytea here), so this script seeds the image
-- with no external file handling at deploy time. The SeedCategories token (next statement) embeds
-- the reference-data file verbatim.
INSERT INTO public.brandassets (assetname, image) VALUES ('Default', {{DefaultLogo}})
ON CONFLICT (assetname) DO NOTHING;

{{SeedCategories}}
