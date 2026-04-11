// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using SchemaSmith.Pro;

namespace Schema.Isolators;

public interface IProDataDelivery
{
    void DeliverTables(DataDeliveryContext context);
}
