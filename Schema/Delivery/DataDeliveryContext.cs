// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System;
using System.Collections.Generic;
using System.Data;

namespace Schema.Delivery;

/// <summary>
/// Runtime context passed from SchemaQuench's DatabaseQuench to an IDataDelivery
/// implementation.
/// </summary>
public class DataDeliveryContext
{
    public IList<IDeliverableTable> Tables { get; set; }
    public string Platform { get; set; }
    public IDbCommand Command { get; set; }
    public string DatabaseName { get; set; }
    public string TemplateRootPath { get; set; }
    public IMergeScriptHelper ScriptHelper { get; set; }
    public Func<string, string> ReadFileContent { get; set; }
    public Action<string, string> ExecuteScript { get; set; }
    public Action<string> ProgressLog { get; set; }
    public Action<string> ProgressLogError { get; set; }
    public bool WhatIf { get; set; }

    /// <summary>
    /// Resolved schema name for the current schema-template iteration. Empty string
    /// for regular templates (the default). When non-empty, DataDeliveryProcessor
    /// substitutes occurrences of <c>"{{SchemaName}}"</c> in each table's Schema
    /// before passing it to the catalog-probing metadata helpers and the
    /// MERGE-script builder. Without this substitution the literal token reaches
    /// the SQL helpers, yielding empty catalog result sets and emitting
    /// <c>MERGE INTO [{{SchemaName}}].[Table]</c> — slice-3 audit bug B3.
    /// </summary>
    public string SchemaName { get; set; } = "";
}
