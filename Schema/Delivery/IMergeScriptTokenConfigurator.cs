// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

namespace Schema.Delivery;

/// <summary>
/// Post-extraction hook that writes the ScriptTokens entry a tokenized merge script needs in
/// order to resolve its {{key}} placeholder. Called by DataTongs after extracting a table's
/// content file, when TokenizeScripts is enabled and a merge script is actually emitted for
/// that table (Issue #390).
/// </summary>
public interface IMergeScriptTokenConfigurator
{
    /// <summary>
    /// Writes (or confirms) a single ScriptTokens entry in the template's Template.json.
    /// </summary>
    /// <param name="context">All inputs needed to locate and update the token.</param>
    void Configure(MergeScriptTokenConfiguratorContext context);
}

/// <summary>
/// Input context for wiring a single table's content-file token into Template.json's ScriptTokens.
/// The token key is pre-resolved by the caller (DataTongs) — this configurator does not derive or
/// schema-qualify the key, it only writes the key it is given (design: "derive the key once").
/// </summary>
public class MergeScriptTokenConfiguratorContext
{
    /// <summary>Template root directory path (Template.json lives here).</summary>
    public string TemplateRootPath { get; set; }

    /// <summary>
    /// The exact ScriptTokens key to write — identical, by construction, to the key embedded in the
    /// generated merge script and to the .tabledata filename stem.
    /// </summary>
    public string TokenKey { get; set; }

    /// <summary>Path to the extracted content file (absolute) the token should point at.</summary>
    public string ContentFilePath { get; set; }

    /// <summary>Callback for logging progress messages.</summary>
    public System.Action<string> ProgressLog { get; set; }

    /// <summary>Callback for logging warning messages.</summary>
    public System.Action<string> WarningLog { get; set; }
}
