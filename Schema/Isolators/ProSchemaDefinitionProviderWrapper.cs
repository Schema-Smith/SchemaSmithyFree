// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using SchemaSmith.Pro;

namespace Schema.Isolators;

public class ProSchemaDefinitionProviderWrapper : IProSchemaDefinitionProvider
{
    public string GetSchemaDefinition(string typeName)
        => ProServices.SchemaDefinitions.GetSchemaDefinition(typeName);

    public static IProSchemaDefinitionProvider GetFromFactory()
        => FactoryContainer.ResolveOrCreate<IProSchemaDefinitionProvider, ProSchemaDefinitionProviderWrapper>();
}
