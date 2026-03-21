// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Schema.DataAccess;
using Schema.Isolators;
using Schema.Utility;

namespace Schema.Domain;

public class SqlScript
{
    public string Name { get; set; }
    public string FilePath { get; set; }
    public List<string> Batches { get; } = [];
    public bool HasBeenQuenched { get; set; }
    public Exception Error { get; set; }
    public string LogPath => LongPathSupport.StripLongPathPrefix(FilePath);

    public static SqlScript Load(string filePath, List<KeyValuePair<string, string>> scriptTokens = null)
    {
        if (!ProductFileWrapper.GetFromFactory().Exists(filePath))
            throw new Exception($"File {LongPathSupport.StripLongPathPrefix(filePath)} does not exist");

        try
        {
            var script = new SqlScript { Name = Path.GetFileName(filePath), FilePath = filePath };
            var batches = SqlHelpers.SplitIntoBatches(ProductFileWrapper.GetFromFactory().ReadAllText(filePath));

            if (scriptTokens != null)
                batches = batches.Select(batch => Product.TokenReplace(batch, scriptTokens)).ToList();

            script.Batches.AddRange(batches);
            return script;
        }
        catch (Exception e)
        {
            throw new Exception($"Error loading {LongPathSupport.StripLongPathPrefix(filePath)}\r\n{e.Message}", e);
        }
    }
}
