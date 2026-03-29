// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using System.Text.RegularExpressions;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace SchemaHammer.Highlighting;

public class TokenTextTransformer : DocumentColorizingTransformer
{
    protected override void ColorizeLine(DocumentLine line)
    {
        var lineText = CurrentContext.Document.GetText(line);
        var matches = TokenHighlightRenderer.TokenPattern().Matches(lineText);
        foreach (Match match in matches)
        {
            var startOffset = line.Offset + match.Index;
            var endOffset = startOffset + match.Length;
            ChangeLinePart(startOffset, endOffset, element =>
            {
                element.TextRunProperties.SetForegroundBrush(Brushes.Black);
                element.TextRunProperties.SetTypeface(new Typeface(
                    element.TextRunProperties.Typeface.FontFamily,
                    FontStyle.Normal,
                    FontWeight.Bold));
            });
        }
    }
}
