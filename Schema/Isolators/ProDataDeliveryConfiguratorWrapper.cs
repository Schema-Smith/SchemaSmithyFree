// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using SchemaSmith.Pro;

namespace Schema.Isolators;

public class ProDataDeliveryConfiguratorWrapper : IProDataDeliveryConfigurator
{
    public void Configure(DataDeliveryConfiguratorContext context)
        => ProServices.DataDeliveryConfigurator.Configure(context);

    public static IProDataDeliveryConfigurator GetFromFactory()
        => FactoryContainer.ResolveOrCreate<IProDataDeliveryConfigurator, ProDataDeliveryConfiguratorWrapper>();
}
