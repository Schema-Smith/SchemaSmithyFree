// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

namespace Schema.Configuration;

/// <summary>
/// Every configuration key the tools read, as constants.
/// <para>Product code reads through these rather than through string literals, so the accepted-key
/// set and the read set are the same symbols instead of two lists that happen to agree today. A
/// guard test scans the product sources and fails if a <c>config["…"]</c> literal names a key that is
/// not registered here, which is what keeps this a contract rather than a promise.</para>
/// </summary>
public static class SettingsKeys
{
    // ---- Shared across tools -------------------------------------------------------------------
    public const string ScriptTokens = "ScriptTokens";

    /// <summary>SchemaQuench deployment target.</summary>
    public static class Target
    {
        public const string Section = "Target";
        public const string Server = "Target:Server";
        public const string User = "Target:User";
        public const string Password = "Target:Password";
        public const string Port = "Target:Port";
        public const string Platform = "Target:Platform";
        public const string IntegratedSecurity = "Target:IntegratedSecurity";
        public const string SecondaryServers = "Target:SecondaryServers";
        public const string ConnectionProperties = "Target:ConnectionProperties";
        public const string Templates = "Target:Templates";
        public const string Databases = "Target:Databases";
        public const string Schemas = "Target:Schemas";
        public const string TemplateTargets = "Target:TemplateTargets";
    }

    /// <summary>SchemaTongs / DataTongs extraction source.</summary>
    public static class Source
    {
        public const string Section = "Source";
        public const string Server = "Source:Server";
        public const string User = "Source:User";
        public const string Password = "Source:Password";
        public const string Port = "Source:Port";
        public const string Platform = "Source:Platform";
        public const string Database = "Source:Database";
        public const string Schema = "Source:Schema";
        public const string IntegratedSecurity = "Source:IntegratedSecurity";
        public const string ConnectionProperties = "Source:ConnectionProperties";
    }

    /// <summary>Product / template identity used by the extraction tools.</summary>
    public static class ProductKeys
    {
        public const string Section = "Product";
        public const string Name = "Product:Name";
        public const string Path = "Product:Path";
        public const string CheckConstraintStyle = "Product:CheckConstraintStyle";
    }

    public static class TemplateKeys
    {
        public const string Section = "Template";
        public const string Name = "Template:Name";
        public const string SchemaIdentificationScript = "Template:SchemaIdentificationScript";
    }

    /// <summary>What SchemaTongs / DataTongs extract.</summary>
    public static class ShouldCast
    {
        public const string Section = "ShouldCast";
        public const string Aggregates = "ShouldCast:Aggregates";
        public const string Catalogs = "ShouldCast:Catalogs";
        public const string CompositeTypes = "ShouldCast:CompositeTypes";
        public const string ConfigureDataDelivery = "ShouldCast:ConfigureDataDelivery";
        public const string DdlTriggers = "ShouldCast:DDLTriggers";
        public const string DeliveryEncoding = "ShouldCast:DeliveryEncoding";
        public const string DisableRules = "ShouldCast:DisableRules";
        public const string DisableTriggers = "ShouldCast:DisableTriggers";
        public const string DomainTypes = "ShouldCast:DomainTypes";
        public const string EnumTypes = "ShouldCast:EnumTypes";
        public const string Events = "ShouldCast:Events";
        public const string Functions = "ShouldCast:Functions";
        public const string IndexedViews = "ShouldCast:IndexedViews";
        public const string MaterializedViews = "ShouldCast:MaterializedViews";
        public const string MergeDelete = "ShouldCast:MergeDelete";
        public const string MergeUpdate = "ShouldCast:MergeUpdate";
        public const string ObjectList = "ShouldCast:ObjectList";
        public const string OutputContentFiles = "ShouldCast:OutputContentFiles";
        public const string OutputScripts = "ShouldCast:OutputScripts";
        public const string Procedures = "ShouldCast:Procedures";
        public const string Rules = "ShouldCast:Rules";
        public const string SaveInvalidScripts = "ShouldCast:SaveInvalidScripts";
        public const string Schemas = "ShouldCast:Schemas";
        public const string ScriptDynamicDependencyRemovalForFunctions = "ShouldCast:ScriptDynamicDependencyRemovalForFunctions";
        public const string Sequences = "ShouldCast:Sequences";
        public const string StopLists = "ShouldCast:StopLists";
        public const string TableTriggers = "ShouldCast:TableTriggers";
        public const string Tables = "ShouldCast:Tables";
        public const string TokenizeScripts = "ShouldCast:TokenizeScripts";
        public const string UpdateDescendents = "ShouldCast:UpdateDescendents";
        public const string UserDefinedTypes = "ShouldCast:UserDefinedTypes";
        public const string ValidateScripts = "ShouldCast:ValidateScripts";
        public const string Views = "ShouldCast:Views";
        public const string XmlSchemaCollections = "ShouldCast:XMLSchemaCollections";
    }

    public static class OrphanHandling
    {
        public const string Section = "OrphanHandling";
        public const string Mode = "OrphanHandling:Mode";
    }

    public const string FolderMapping = "FolderMapping";
    public const string LogHygiene = "LogHygiene";

    // ---- SchemaQuench top-level ------------------------------------------------------------------
    public const string SchemaPackagePath = "SchemaPackagePath";
    public const string MaxThreads = "MaxThreads";
    public const string WhatIfOnly = "WhatIfONLY";
    public const string RunScriptsTwice = "RunScriptsTwice";
    public const string DropTablesRemovedFromProduct = "DropTablesRemovedFromProduct";
    public const string DropUnknownIndexes = "DropUnknownIndexes";
    public const string DropColumnsRemovedFromProduct = "DropColumnsRemovedFromProduct";
    public const string DropForeignKeysRemovedFromProduct = "DropForeignKeysRemovedFromProduct";
    public const string DropCheckConstraintsRemovedFromProduct = "DropCheckConstraintsRemovedFromProduct";
    public const string DropExcludeConstraintsRemovedFromProduct = "DropExcludeConstraintsRemovedFromProduct";
    public const string DropStatisticsRemovedFromProduct = "DropStatisticsRemovedFromProduct";
    public const string DropIndexesRemovedFromProduct = "DropIndexesRemovedFromProduct";
    public const string PreventDrop = "PreventDrop";
    public const string UpdateTables = "UpdateTables";
    public const string KindleTheForge = "KindleTheForge";
    public const string ForceReKindle = "ForceReKindle";
    public const string CheckpointDirectory = "CheckpointDirectory";
    public const string DeliverData = "DeliverData";
    public const string VerboseLogging = "VerboseLogging";
    public const string FailureContextLines = "FailureContextLines";
    public const string BottleneckThresholdMs = "BottleneckThresholdMs";
    public const string TrackRunOnceMigrations = "TrackRunOnceMigrations";
    public const string PruneObsoleteMigrationTracking = "PruneObsoleteMigrationTracking";
    public const string MinimumVersion = "MinimumVersion";
    public const string UnsupportedFeaturePolicy = "Target:UnsupportedFeaturePolicy";
    public const string CompatEncoding = "Target:CompatEncoding";
    public const string SourceCompatEncoding = "Source:CompatEncoding";
    public const string ArtifactPath = "ArtifactPath";
    public const string ScrubArtifacts = "ScrubArtifacts";

    // ---- DataTongs -------------------------------------------------------------------------------
    public const string ContentPath = "ContentPath";
    public const string ScriptPath = "ScriptPath";
    public const string TemplatePath = "TemplatePath";
    /// <summary>DataTongs' list of tables to extract (array-valued).</summary>
    public const string TablesToExtract = "Tables";

    // ---- SchemaShears ----------------------------------------------------------------------------
    public const string SourcePath = "SourcePath";
    public const string ManifestPath = "ManifestPath";
    public const string AlwaysIncludePath = "AlwaysIncludePath";
    public const string OutputPath = "OutputPath";
    public const string Zip = "Zip";
    public const string AllowDrops = "AllowDrops";

    // ---- Test/diagnostic ------------------------------------------------------------------------
    public const string CustomKey = "CustomKey";
}
