// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Reflection;
using System.Xml;
using AvaloniaEdit.Highlighting;
using AvaloniaEdit.Highlighting.Xshd;

namespace SchemaHammer.Highlighting;

public static class SqlEditorSetup
{
    private static IHighlightingDefinition? _tsqlHighlighting;

    public static IHighlightingDefinition GetTSqlHighlighting()
    {
        if (_tsqlHighlighting != null) return _tsqlHighlighting;

        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("SchemaHammer.Highlighting.TSQL.xshd")
            ?? throw new InvalidOperationException("TSQL.xshd not found as embedded resource");
        using var reader = XmlReader.Create(stream);
        _tsqlHighlighting = HighlightingLoader.Load(reader, HighlightingManager.Instance);
        return _tsqlHighlighting;
    }
}
