// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using SchemaSmith.Pro;

namespace Schema.Isolators;

public class ProDataDeliveryWrapper : IProDataDelivery
{
    public void DeliverTables(DataDeliveryContext context)
        => ProServices.DataDelivery.DeliverTables(context);

    public static IProDataDelivery GetFromFactory()
        => FactoryContainer.ResolveOrCreate<IProDataDelivery, ProDataDeliveryWrapper>();
}
