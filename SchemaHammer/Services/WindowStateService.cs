// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

namespace SchemaHammer.Services;

public static class WindowStateService
{
    public record WindowPosition(double X, double Y, double Width, double Height, bool IsMaximized);

    public record ScreenBounds(double Width, double Height);

    /// <summary>
    /// Calculates the window position to restore, clamping to screen bounds when available.
    /// Returns null when saved dimensions are invalid (zero or negative).
    /// </summary>
    public static WindowPosition? CalculateRestoredPosition(
        double savedX, double savedY, double savedWidth, double savedHeight,
        bool isMaximized, double minWidth, double minHeight,
        ScreenBounds? screen)
    {
        if (savedWidth <= 0 || savedHeight <= 0)
            return null;

        var width = Math.Max(savedWidth, minWidth);
        var height = Math.Max(savedHeight, minHeight);

        if (isMaximized)
            return new WindowPosition(savedX, savedY, width, height, IsMaximized: true);

        if (screen != null)
        {
            var x = Math.Max(0, Math.Min(savedX, screen.Width - width));
            var y = Math.Max(0, Math.Min(savedY, screen.Height - height));
            return new WindowPosition(x, y, width, height, IsMaximized: false);
        }

        return new WindowPosition(savedX, savedY, width, height, IsMaximized: false);
    }

    /// <summary>
    /// Determines the window state to save on close.
    /// Returns null when the window is minimized (previous normal bounds should be kept).
    /// </summary>
    public static WindowPosition? CalculateSaveState(
        bool isMinimized, bool isMaximized,
        double x, double y, double width, double height)
    {
        if (isMinimized)
            return null;

        return new WindowPosition(x, y, width, height, isMaximized);
    }
}
