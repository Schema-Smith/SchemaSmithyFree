// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

namespace Schema.Delivery;

/// <summary>
/// FK-aware data delivery during deployment. Reads each table's DataDelivery
/// configuration, orders by FK dependencies, and executes merge scripts.
/// </summary>
public interface IDataDelivery
{
    void DeliverTables(DataDeliveryContext context);
}
