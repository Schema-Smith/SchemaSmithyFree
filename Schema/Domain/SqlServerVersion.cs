// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Schema.Domain;

[JsonConverter(typeof(StringEnumConverter))]
public enum SqlServerVersion
{
    Sql2016 = 2016,
    Sql2017 = 2017,
    Sql2019 = 2019,
    Sql2022 = 2022,
    Sql2025 = 2025
}
