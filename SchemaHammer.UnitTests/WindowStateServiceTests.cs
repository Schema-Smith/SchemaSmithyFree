// Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

using SchemaHammer.Services;

namespace SchemaHammer.UnitTests;

public class WindowStateServiceTests
{
    private const double MinWidth = 400;
    private const double MinHeight = 300;

    #region CalculateRestoredPosition

    [Test]
    public void CalculateRestoredPosition_NormalBoundsWithinScreen_ReturnedAsIs()
    {
        var screen = new WindowStateService.ScreenBounds(1920, 1080);

        var result = WindowStateService.CalculateRestoredPosition(
            100, 100, 800, 600, false, MinWidth, MinHeight, screen);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.X, Is.EqualTo(100));
        Assert.That(result.Y, Is.EqualTo(100));
        Assert.That(result.Width, Is.EqualTo(800));
        Assert.That(result.Height, Is.EqualTo(600));
        Assert.That(result.IsMaximized, Is.False);
    }

    [Test]
    public void CalculateRestoredPosition_PartiallyOffScreenRight_ClampedToScreenEdge()
    {
        var screen = new WindowStateService.ScreenBounds(1920, 1080);

        var result = WindowStateService.CalculateRestoredPosition(
            1800, 100, 800, 600, false, MinWidth, MinHeight, screen);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.X, Is.EqualTo(1120)); // 1920 - 800
        Assert.That(result.Y, Is.EqualTo(100));
    }

    [Test]
    public void CalculateRestoredPosition_PartiallyOffScreenBottom_ClampedToScreenEdge()
    {
        var screen = new WindowStateService.ScreenBounds(1920, 1080);

        var result = WindowStateService.CalculateRestoredPosition(
            100, 900, 800, 600, false, MinWidth, MinHeight, screen);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.X, Is.EqualTo(100));
        Assert.That(result.Y, Is.EqualTo(480)); // 1080 - 600
    }

    [Test]
    public void CalculateRestoredPosition_NegativeSavedPosition_ClampedToZero()
    {
        var screen = new WindowStateService.ScreenBounds(1920, 1080);

        var result = WindowStateService.CalculateRestoredPosition(
            -50, -100, 800, 600, false, MinWidth, MinHeight, screen);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.X, Is.EqualTo(0));
        Assert.That(result.Y, Is.EqualTo(0));
    }

    [Test]
    public void CalculateRestoredPosition_CompletelyOffScreen_ClampedToOrigin()
    {
        var screen = new WindowStateService.ScreenBounds(1920, 1080);

        var result = WindowStateService.CalculateRestoredPosition(
            5000, 5000, 800, 600, false, MinWidth, MinHeight, screen);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.X, Is.EqualTo(1120)); // 1920 - 800
        Assert.That(result.Y, Is.EqualTo(480));   // 1080 - 600
    }

    [Test]
    public void CalculateRestoredPosition_Maximized_ReturnsMaximizedWithDimensions()
    {
        var screen = new WindowStateService.ScreenBounds(1920, 1080);

        var result = WindowStateService.CalculateRestoredPosition(
            100, 100, 800, 600, true, MinWidth, MinHeight, screen);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.IsMaximized, Is.True);
        Assert.That(result.Width, Is.EqualTo(800));
        Assert.That(result.Height, Is.EqualTo(600));
    }

    [Test]
    public void CalculateRestoredPosition_Maximized_DoesNotClampPosition()
    {
        var screen = new WindowStateService.ScreenBounds(1920, 1080);

        var result = WindowStateService.CalculateRestoredPosition(
            5000, 5000, 800, 600, true, MinWidth, MinHeight, screen);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.X, Is.EqualTo(5000));
        Assert.That(result.Y, Is.EqualTo(5000));
    }

    [Test]
    public void CalculateRestoredPosition_NoScreenInfo_ReturnsSavedPositionUnclamped()
    {
        var result = WindowStateService.CalculateRestoredPosition(
            100, 200, 800, 600, false, MinWidth, MinHeight, null);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.X, Is.EqualTo(100));
        Assert.That(result.Y, Is.EqualTo(200));
        Assert.That(result.Width, Is.EqualTo(800));
        Assert.That(result.Height, Is.EqualTo(600));
    }

    [Test]
    public void CalculateRestoredPosition_ZeroWidth_ReturnsNull()
    {
        var result = WindowStateService.CalculateRestoredPosition(
            100, 100, 0, 600, false, MinWidth, MinHeight, null);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void CalculateRestoredPosition_ZeroHeight_ReturnsNull()
    {
        var result = WindowStateService.CalculateRestoredPosition(
            100, 100, 800, 0, false, MinWidth, MinHeight, null);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void CalculateRestoredPosition_NegativeDimensions_ReturnsNull()
    {
        var result = WindowStateService.CalculateRestoredPosition(
            100, 100, -800, -600, false, MinWidth, MinHeight, null);

        Assert.That(result, Is.Null);
    }

    [Test]
    public void CalculateRestoredPosition_DimensionsBelowMinimum_ClampsToMinimum()
    {
        var result = WindowStateService.CalculateRestoredPosition(
            100, 100, 200, 150, false, MinWidth, MinHeight, null);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Width, Is.EqualTo(MinWidth));
        Assert.That(result.Height, Is.EqualTo(MinHeight));
    }

    #endregion

    #region CalculateSaveState

    [Test]
    public void CalculateSaveState_NormalWindow_ReturnsSaveData()
    {
        var result = WindowStateService.CalculateSaveState(
            false, false, 100, 200, 800, 600);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.X, Is.EqualTo(100));
        Assert.That(result.Y, Is.EqualTo(200));
        Assert.That(result.Width, Is.EqualTo(800));
        Assert.That(result.Height, Is.EqualTo(600));
        Assert.That(result.IsMaximized, Is.False);
    }

    [Test]
    public void CalculateSaveState_MaximizedWindow_ReturnsSaveDataWithMaximizedFlag()
    {
        var result = WindowStateService.CalculateSaveState(
            false, true, 0, 0, 1920, 1080);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.IsMaximized, Is.True);
    }

    [Test]
    public void CalculateSaveState_MinimizedWindow_ReturnsNull()
    {
        var result = WindowStateService.CalculateSaveState(
            true, false, 100, 200, 800, 600);

        Assert.That(result, Is.Null);
    }

    #endregion
}
