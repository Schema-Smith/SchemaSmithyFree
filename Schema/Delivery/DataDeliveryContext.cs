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
}
