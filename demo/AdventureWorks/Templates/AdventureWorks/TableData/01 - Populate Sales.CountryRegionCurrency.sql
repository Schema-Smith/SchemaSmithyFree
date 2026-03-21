DECLARE @data VARCHAR(MAX) = '[
    {
        "CountryRegionCode": "AE",
        "CurrencyCode": "AED",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "AR",
        "CurrencyCode": "ARS",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "AT",
        "CurrencyCode": "ATS",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "AT",
        "CurrencyCode": "EUR",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "CountryRegionCode": "AU",
        "CurrencyCode": "AUD",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "BB",
        "CurrencyCode": "BBD",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "BD",
        "CurrencyCode": "BDT",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "BE",
        "CurrencyCode": "BEF",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "BE",
        "CurrencyCode": "EUR",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "CountryRegionCode": "BG",
        "CurrencyCode": "BGN",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "BH",
        "CurrencyCode": "BHD",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "BN",
        "CurrencyCode": "BND",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "BO",
        "CurrencyCode": "BOB",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "BR",
        "CurrencyCode": "BRL",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "BS",
        "CurrencyCode": "BSD",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "BT",
        "CurrencyCode": "BTN",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "CA",
        "CurrencyCode": "CAD",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "CH",
        "CurrencyCode": "CHF",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "CL",
        "CurrencyCode": "CLP",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "CN",
        "CurrencyCode": "CNY",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "CO",
        "CurrencyCode": "COP",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "CR",
        "CurrencyCode": "CRC",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "CY",
        "CurrencyCode": "CYP",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "CZ",
        "CurrencyCode": "CZK",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "DE",
        "CurrencyCode": "DEM",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "DE",
        "CurrencyCode": "EUR",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "DK",
        "CurrencyCode": "DKK",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "DO",
        "CurrencyCode": "DOP",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "DZ",
        "CurrencyCode": "DZD",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "EC",
        "CurrencyCode": "USD",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "EE",
        "CurrencyCode": "EEK",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "EG",
        "CurrencyCode": "EGP",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "ES",
        "CurrencyCode": "ESP",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "ES",
        "CurrencyCode": "EUR",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "CountryRegionCode": "FI",
        "CurrencyCode": "EUR",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "CountryRegionCode": "FI",
        "CurrencyCode": "FIM",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "FJ",
        "CurrencyCode": "FJD",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "FR",
        "CurrencyCode": "EUR",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "FR",
        "CurrencyCode": "FRF",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "GB",
        "CurrencyCode": "GBP",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "GH",
        "CurrencyCode": "GHC",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "GR",
        "CurrencyCode": "EUR",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "CountryRegionCode": "GR",
        "CurrencyCode": "GRD",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "GT",
        "CurrencyCode": "GTQ",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "HK",
        "CurrencyCode": "HKD",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "HR",
        "CurrencyCode": "HRK",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "HU",
        "CurrencyCode": "HUF",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "ID",
        "CurrencyCode": "IDR",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "IE",
        "CurrencyCode": "EUR",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "CountryRegionCode": "IE",
        "CurrencyCode": "IEP",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "IL",
        "CurrencyCode": "ILS",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "IN",
        "CurrencyCode": "INR",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "IS",
        "CurrencyCode": "ISK",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "IT",
        "CurrencyCode": "EUR",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "CountryRegionCode": "IT",
        "CurrencyCode": "ITL",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "JM",
        "CurrencyCode": "JMD",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "JO",
        "CurrencyCode": "JOD",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "JP",
        "CurrencyCode": "JPY",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "KE",
        "CurrencyCode": "KES",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "KR",
        "CurrencyCode": "KRW",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "KW",
        "CurrencyCode": "KWD",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "LB",
        "CurrencyCode": "LBP",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "LK",
        "CurrencyCode": "LKR",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "LT",
        "CurrencyCode": "LTL",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "LU",
        "CurrencyCode": "EUR",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "CountryRegionCode": "LV",
        "CurrencyCode": "LVL",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "MA",
        "CurrencyCode": "MAD",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "MT",
        "CurrencyCode": "MTL",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "MU",
        "CurrencyCode": "MUR",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "MV",
        "CurrencyCode": "MVR",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "MX",
        "CurrencyCode": "MXN",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "MY",
        "CurrencyCode": "MYR",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "NA",
        "CurrencyCode": "NAD",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "NG",
        "CurrencyCode": "NGN",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "NL",
        "CurrencyCode": "EUR",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "CountryRegionCode": "NL",
        "CurrencyCode": "NLG",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "NO",
        "CurrencyCode": "NOK",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "NP",
        "CurrencyCode": "NPR",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "NZ",
        "CurrencyCode": "NZD",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "OM",
        "CurrencyCode": "OMR",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "PA",
        "CurrencyCode": "PAB",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "PE",
        "CurrencyCode": "PEN",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "PH",
        "CurrencyCode": "PHP",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "PK",
        "CurrencyCode": "PKR",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "PL",
        "CurrencyCode": "PLN",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "PL",
        "CurrencyCode": "PLZ",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "PT",
        "CurrencyCode": "EUR",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "CountryRegionCode": "PT",
        "CurrencyCode": "PTE",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "PY",
        "CurrencyCode": "PYG",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "RO",
        "CurrencyCode": "ROL",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "RU",
        "CurrencyCode": "RUB",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "RU",
        "CurrencyCode": "RUR",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "SA",
        "CurrencyCode": "SAR",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "SE",
        "CurrencyCode": "SEK",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "SG",
        "CurrencyCode": "SGD",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "SI",
        "CurrencyCode": "SIT",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "SK",
        "CurrencyCode": "SKK",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "SV",
        "CurrencyCode": "SVC",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "TH",
        "CurrencyCode": "THB",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "TN",
        "CurrencyCode": "TND",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "TR",
        "CurrencyCode": "TRL",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "TT",
        "CurrencyCode": "TTD",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "TW",
        "CurrencyCode": "TWD",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "US",
        "CurrencyCode": "USD",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "UY",
        "CurrencyCode": "UYU",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "VE",
        "CurrencyCode": "VEB",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "VN",
        "CurrencyCode": "VND",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "ZA",
        "CurrencyCode": "ZAR",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    },
    {
        "CountryRegionCode": "ZW",
        "CurrencyCode": "ZWD",
        "ModifiedDate": "2014-02-08T10:17:21.510"
    }
]'

MERGE INTO Sales.CountryRegionCurrency AS Target
USING (
    SELECT *
    FROM OPENJSON(@data)
    WITH (
        CountryRegionCode nvarchar(3),
        CurrencyCode nchar(3),
        ModifiedDate datetime
    )
) AS Source
ON Target.CountryRegionCode = Source.CountryRegionCode 
   AND Target.CurrencyCode = Source.CurrencyCode
WHEN MATCHED THEN
    UPDATE SET
        ModifiedDate = Source.ModifiedDate
WHEN NOT MATCHED THEN
    INSERT (CountryRegionCode, CurrencyCode, ModifiedDate)
    VALUES (Source.CountryRegionCode, Source.CurrencyCode, Source.ModifiedDate);
    