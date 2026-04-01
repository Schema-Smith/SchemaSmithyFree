// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Text.RegularExpressions;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace SchemaHammer.Highlighting;

public partial class TokenHighlightRenderer : IBackgroundRenderer
{
    private static readonly IBrush Background = new ImmutableSolidColorBrush(Color.FromRgb(255, 255, 0));
    private static readonly Pen Border = new(new ImmutableSolidColorBrush(Color.FromRgb(200, 200, 0)), 1);

    private readonly TextEditor _editor;

    public TokenHighlightRenderer(TextEditor editor)
    {
        _editor = editor;
    }

    public KnownLayer Layer => KnownLayer.Selection;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (_editor.Document == null) return;

        var visualLines = textView.VisualLines;
        if (visualLines.Count == 0) return;

        var firstLine = visualLines[0].FirstDocumentLine.LineNumber;
        var lastLine = visualLines[^1].LastDocumentLine.LineNumber;

        for (var line = firstLine; line <= lastLine; line++)
        {
            var docLine = _editor.Document.GetLineByNumber(line);
            var lineText = _editor.Document.GetText(docLine.Offset, docLine.Length);

            var matches = TokenPattern().Matches(lineText);
            foreach (Match match in matches)
            {
                var startOffset = docLine.Offset + match.Index;
                var endOffset = startOffset + match.Length;

                var segment = new TextSegment { StartOffset = startOffset, EndOffset = endOffset };
                foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
                {
                    drawingContext.DrawRectangle(Background, Border,
                        new Avalonia.Rect(rect.X, rect.Y, rect.Width, rect.Height));
                }
            }
        }
    }

    [GeneratedRegex(@"\{\{\S+?\}\}")]
    internal static partial Regex TokenPattern();
}
