using System;
using System.Collections.Generic;
using System.IO;
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

    public static SqlScript Load(string filePath)
    {
        if (!FileWrapper.GetFromFactory().Exists(filePath))
            throw new Exception($"File {LongPathSupport.StripLongPathPrefix(filePath)} does not exist");

        try
        {
            var script = new SqlScript { Name = Path.GetFileName(filePath), FilePath = filePath };
            script.Batches.AddRange(SqlHelpers.SplitIntoBatches(FileWrapper.GetFromFactory().ReadAllText(filePath)));
            return script;
        }
        catch (Exception e)
        {
            throw new Exception($"Error loading {LongPathSupport.StripLongPathPrefix(filePath)}\r\n{e.Message}", e);
        }
    }
}