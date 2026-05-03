// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

namespace Schema.Checkpointing;

/// <summary>
/// Identifies the deployment context for checkpoint tracking. Product-level tracking
/// sets only ProductName; database-level tracking sets all four fields. The fields
/// are used to construct a unique key for state storage.
/// </summary>
public class TrackingScope
{
    public string ProductName { get; set; }
    public string TemplateName { get; set; }
    public string Server { get; set; }
    public string DatabaseName { get; set; }
}
