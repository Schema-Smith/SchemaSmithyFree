
DECLARE @v_json NVARCHAR(MAX) = '[
{"CurrencyCode":"AED","ModifiedDate":"2008-04-30T00:00:00","Name":"Emirati Dirham"},
{"CurrencyCode":"AFA","ModifiedDate":"2008-04-30T00:00:00","Name":"Afghani"},
{"CurrencyCode":"ALL","ModifiedDate":"2008-04-30T00:00:00","Name":"Lek"},
{"CurrencyCode":"AMD","ModifiedDate":"2008-04-30T00:00:00","Name":"Armenian Dram"},
{"CurrencyCode":"ANG","ModifiedDate":"2008-04-30T00:00:00","Name":"Netherlands Antillian Guilder"},
{"CurrencyCode":"AOA","ModifiedDate":"2008-04-30T00:00:00","Name":"Kwanza"},
{"CurrencyCode":"ARS","ModifiedDate":"2008-04-30T00:00:00","Name":"Argentine Peso"},
{"CurrencyCode":"ATS","ModifiedDate":"2008-04-30T00:00:00","Name":"Shilling"},
{"CurrencyCode":"AUD","ModifiedDate":"2008-04-30T00:00:00","Name":"Australian Dollar"},
{"CurrencyCode":"AWG","ModifiedDate":"2008-04-30T00:00:00","Name":"Aruban Guilder"},
{"CurrencyCode":"AZM","ModifiedDate":"2008-04-30T00:00:00","Name":"Azerbaijanian Manat"},
{"CurrencyCode":"BBD","ModifiedDate":"2008-04-30T00:00:00","Name":"Barbados Dollar"},
{"CurrencyCode":"BDT","ModifiedDate":"2008-04-30T00:00:00","Name":"Taka"},
{"CurrencyCode":"BEF","ModifiedDate":"2008-04-30T00:00:00","Name":"Belgian Franc"},
{"CurrencyCode":"BGN","ModifiedDate":"2008-04-30T00:00:00","Name":"Bulgarian Lev"},
{"CurrencyCode":"BHD","ModifiedDate":"2008-04-30T00:00:00","Name":"Bahraini Dinar"},
{"CurrencyCode":"BND","ModifiedDate":"2008-04-30T00:00:00","Name":"Brunei Dollar"},
{"CurrencyCode":"BOB","ModifiedDate":"2008-04-30T00:00:00","Name":"Boliviano"},
{"CurrencyCode":"BRL","ModifiedDate":"2008-04-30T00:00:00","Name":"Brazilian Real"},
{"CurrencyCode":"BSD","ModifiedDate":"2008-04-30T00:00:00","Name":"Bahamian Dollar"},
{"CurrencyCode":"BTN","ModifiedDate":"2008-04-30T00:00:00","Name":"Ngultrum"},
{"CurrencyCode":"CAD","ModifiedDate":"2008-04-30T00:00:00","Name":"Canadian Dollar"},
{"CurrencyCode":"CHF","ModifiedDate":"2008-04-30T00:00:00","Name":"Swiss Franc"},
{"CurrencyCode":"CLP","ModifiedDate":"2008-04-30T00:00:00","Name":"Chilean Peso"},
{"CurrencyCode":"CNY","ModifiedDate":"2008-04-30T00:00:00","Name":"Yuan Renminbi"},
{"CurrencyCode":"COP","ModifiedDate":"2008-04-30T00:00:00","Name":"Colombian Peso"},
{"CurrencyCode":"CRC","ModifiedDate":"2008-04-30T00:00:00","Name":"Costa Rican Colon"},
{"CurrencyCode":"CYP","ModifiedDate":"2008-04-30T00:00:00","Name":"Cyprus Pound"},
{"CurrencyCode":"CZK","ModifiedDate":"2008-04-30T00:00:00","Name":"Czech Koruna"},
{"CurrencyCode":"DEM","ModifiedDate":"2008-04-30T00:00:00","Name":"Deutsche Mark"},
{"CurrencyCode":"DKK","ModifiedDate":"2008-04-30T00:00:00","Name":"Danish Krone"},
{"CurrencyCode":"DOP","ModifiedDate":"2008-04-30T00:00:00","Name":"Dominican Peso"},
{"CurrencyCode":"DZD","ModifiedDate":"2008-04-30T00:00:00","Name":"Algerian Dinar"},
{"CurrencyCode":"EEK","ModifiedDate":"2008-04-30T00:00:00","Name":"Kroon"},
{"CurrencyCode":"EGP","ModifiedDate":"2008-04-30T00:00:00","Name":"Egyptian Pound"},
{"CurrencyCode":"ESP","ModifiedDate":"2008-04-30T00:00:00","Name":"Spanish Peseta"},
{"CurrencyCode":"EUR","ModifiedDate":"2008-04-30T00:00:00","Name":"EURO"},
{"CurrencyCode":"FIM","ModifiedDate":"2008-04-30T00:00:00","Name":"Markka"},
{"CurrencyCode":"FJD","ModifiedDate":"2008-04-30T00:00:00","Name":"Fiji Dollar"},
{"CurrencyCode":"FRF","ModifiedDate":"2008-04-30T00:00:00","Name":"French Franc"},
{"CurrencyCode":"GBP","ModifiedDate":"2008-04-30T00:00:00","Name":"United Kingdom Pound"},
{"CurrencyCode":"GHC","ModifiedDate":"2008-04-30T00:00:00","Name":"Cedi"},
{"CurrencyCode":"GRD","ModifiedDate":"2008-04-30T00:00:00","Name":"Drachma"},
{"CurrencyCode":"GTQ","ModifiedDate":"2008-04-30T00:00:00","Name":"Quetzal"},
{"CurrencyCode":"HKD","ModifiedDate":"2008-04-30T00:00:00","Name":"Hong Kong Dollar"},
{"CurrencyCode":"HRK","ModifiedDate":"2008-04-30T00:00:00","Name":"Croatian Kuna"},
{"CurrencyCode":"HUF","ModifiedDate":"2008-04-30T00:00:00","Name":"Forint"},
{"CurrencyCode":"IDR","ModifiedDate":"2008-04-30T00:00:00","Name":"Rupiah"},
{"CurrencyCode":"IEP","ModifiedDate":"2008-04-30T00:00:00","Name":"Irish Pound"},
{"CurrencyCode":"ILS","ModifiedDate":"2008-04-30T00:00:00","Name":"New Israeli Shekel"},
{"CurrencyCode":"INR","ModifiedDate":"2008-04-30T00:00:00","Name":"Indian Rupee"},
{"CurrencyCode":"ISK","ModifiedDate":"2008-04-30T00:00:00","Name":"Iceland Krona"},
{"CurrencyCode":"ITL","ModifiedDate":"2008-04-30T00:00:00","Name":"Italian Lira"},
{"CurrencyCode":"JMD","ModifiedDate":"2008-04-30T00:00:00","Name":"Jamaican Dollar"},
{"CurrencyCode":"JOD","ModifiedDate":"2008-04-30T00:00:00","Name":"Jordanian Dinar"},
{"CurrencyCode":"JPY","ModifiedDate":"2008-04-30T00:00:00","Name":"Yen"},
{"CurrencyCode":"KES","ModifiedDate":"2008-04-30T00:00:00","Name":"Kenyan Shilling"},
{"CurrencyCode":"KRW","ModifiedDate":"2008-04-30T00:00:00","Name":"Won"},
{"CurrencyCode":"KWD","ModifiedDate":"2008-04-30T00:00:00","Name":"Kuwaiti Dinar"},
{"CurrencyCode":"LBP","ModifiedDate":"2008-04-30T00:00:00","Name":"Lebanese Pound"},
{"CurrencyCode":"LKR","ModifiedDate":"2008-04-30T00:00:00","Name":"Sri Lankan Rupee"},
{"CurrencyCode":"LTL","ModifiedDate":"2008-04-30T00:00:00","Name":"Lithuanian Litas"},
{"CurrencyCode":"LVL","ModifiedDate":"2008-04-30T00:00:00","Name":"Latvian Lats"},
{"CurrencyCode":"MAD","ModifiedDate":"2008-04-30T00:00:00","Name":"Moroccan Dirham"},
{"CurrencyCode":"MTL","ModifiedDate":"2008-04-30T00:00:00","Name":"Maltese Lira"},
{"CurrencyCode":"MUR","ModifiedDate":"2008-04-30T00:00:00","Name":"Mauritius Rupee"},
{"CurrencyCode":"MVR","ModifiedDate":"2008-04-30T00:00:00","Name":"Rufiyaa"},
{"CurrencyCode":"MXN","ModifiedDate":"2008-04-30T00:00:00","Name":"Mexican Peso"},
{"CurrencyCode":"MYR","ModifiedDate":"2008-04-30T00:00:00","Name":"Malaysian Ringgit"},
{"CurrencyCode":"NAD","ModifiedDate":"2008-04-30T00:00:00","Name":"Namibia Dollar"},
{"CurrencyCode":"NGN","ModifiedDate":"2008-04-30T00:00:00","Name":"Naira"},
{"CurrencyCode":"NLG","ModifiedDate":"2008-04-30T00:00:00","Name":"Netherlands Guilder"},
{"CurrencyCode":"NOK","ModifiedDate":"2008-04-30T00:00:00","Name":"Norwegian Krone"},
{"CurrencyCode":"NPR","ModifiedDate":"2008-04-30T00:00:00","Name":"Nepalese Rupee"},
{"CurrencyCode":"NZD","ModifiedDate":"2008-04-30T00:00:00","Name":"New Zealand Dollar"},
{"CurrencyCode":"OMR","ModifiedDate":"2008-04-30T00:00:00","Name":"Omani Rial"},
{"CurrencyCode":"PAB","ModifiedDate":"2008-04-30T00:00:00","Name":"Balboa"},
{"CurrencyCode":"PEN","ModifiedDate":"2008-04-30T00:00:00","Name":"Nuevo Sol"},
{"CurrencyCode":"PHP","ModifiedDate":"2008-04-30T00:00:00","Name":"Philippine Peso"},
{"CurrencyCode":"PKR","ModifiedDate":"2008-04-30T00:00:00","Name":"Pakistan Rupee"},
{"CurrencyCode":"PLN","ModifiedDate":"2008-04-30T00:00:00","Name":"Zloty"},
{"CurrencyCode":"PLZ","ModifiedDate":"2008-04-30T00:00:00","Name":"Polish Zloty(old)"},
{"CurrencyCode":"PTE","ModifiedDate":"2008-04-30T00:00:00","Name":"Portuguese Escudo"},
{"CurrencyCode":"PYG","ModifiedDate":"2008-04-30T00:00:00","Name":"Guarani"},
{"CurrencyCode":"ROL","ModifiedDate":"2008-04-30T00:00:00","Name":"Leu"},
{"CurrencyCode":"RUB","ModifiedDate":"2008-04-30T00:00:00","Name":"Russian Ruble"},
{"CurrencyCode":"RUR","ModifiedDate":"2008-04-30T00:00:00","Name":"Russian Ruble(old)"},
{"CurrencyCode":"SAR","ModifiedDate":"2008-04-30T00:00:00","Name":"Saudi Riyal"},
{"CurrencyCode":"SEK","ModifiedDate":"2008-04-30T00:00:00","Name":"Swedish Krona"},
{"CurrencyCode":"SGD","ModifiedDate":"2008-04-30T00:00:00","Name":"Singapore Dollar"},
{"CurrencyCode":"SIT","ModifiedDate":"2008-04-30T00:00:00","Name":"Tolar"},
{"CurrencyCode":"SKK","ModifiedDate":"2008-04-30T00:00:00","Name":"Slovak Koruna"},
{"CurrencyCode":"SVC","ModifiedDate":"2008-04-30T00:00:00","Name":"El Salvador Colon"},
{"CurrencyCode":"THB","ModifiedDate":"2008-04-30T00:00:00","Name":"Baht"},
{"CurrencyCode":"TND","ModifiedDate":"2008-04-30T00:00:00","Name":"Tunisian Dinar"},
{"CurrencyCode":"TRL","ModifiedDate":"2008-04-30T00:00:00","Name":"Turkish Lira"},
{"CurrencyCode":"TTD","ModifiedDate":"2008-04-30T00:00:00","Name":"Trinidad and Tobago Dollar"},
{"CurrencyCode":"TWD","ModifiedDate":"2008-04-30T00:00:00","Name":"New Taiwan Dollar"},
{"CurrencyCode":"USD","ModifiedDate":"2008-04-30T00:00:00","Name":"US Dollar"},
{"CurrencyCode":"UYU","ModifiedDate":"2008-04-30T00:00:00","Name":"Uruguayan Peso"},
{"CurrencyCode":"VEB","ModifiedDate":"2008-04-30T00:00:00","Name":"Bolivar"},
{"CurrencyCode":"VND","ModifiedDate":"2008-04-30T00:00:00","Name":"Dong"},
{"CurrencyCode":"XOF","ModifiedDate":"2008-04-30T00:00:00","Name":"CFA Franc BCEAO"},
{"CurrencyCode":"ZAR","ModifiedDate":"2008-04-30T00:00:00","Name":"Rand"},
{"CurrencyCode":"ZWD","ModifiedDate":"2008-04-30T00:00:00","Name":"Zimbabwe Dollar"}
]';

ALTER TABLE [Sales].[Currency] DISABLE TRIGGER ALL;
 
MERGE INTO [Sales].[Currency] AS Target
USING (
  SELECT [CurrencyCode],[ModifiedDate],[Name]
    FROM OPENJSON(@v_json)
    WITH (
           [CurrencyCode] NCHAR(3),
           [ModifiedDate] DATETIME,
           [Name] NAME
    )
) AS Source
ON Source.[CurrencyCode] = Target.[CurrencyCode]


WHEN MATCHED AND (NOT (Target.[CurrencyCode] = Source.[CurrencyCode] OR (Target.[CurrencyCode] IS NULL AND Source.[CurrencyCode] IS NULL)) AND NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) AND NOT (Target.[Name] = Source.[Name] OR (Target.[Name] IS NULL AND Source.[Name] IS NULL))) THEN
  UPDATE SET
        [CurrencyCode] = Source.[CurrencyCode],
        [ModifiedDate] = Source.[ModifiedDate],
        [Name] = Source.[Name]


WHEN NOT MATCHED THEN
  INSERT (
        [CurrencyCode],
        [ModifiedDate],
        [Name]
  ) VALUES (
        Source.[CurrencyCode],
        Source.[ModifiedDate],
        Source.[Name]  
  )
;
ALTER TABLE [Sales].[Currency] ENABLE TRIGGER ALL;
