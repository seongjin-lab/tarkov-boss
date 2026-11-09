using System.Windows;
using Xunit;

namespace BossMonitor.Tests;

public class WindowPlacementTests
{
    [Theory]
    [InlineData(1920, 250)]
    [InlineData(-1600, -850)]
    public void GrowingWindowStaysInsideItsMonitor(int screenLeft, int screenTop)
    {
        var work = new Rect(screenLeft, screenTop, 1600, 850);
        var window = new Rect(screenLeft + 800, screenTop + 390, 700, 450);

        var fitted = WindowPlacement.ResizeWithinWorkArea(window, new Size(900, 700), work);

        Assert.Equal(new Rect(screenLeft + 700, screenTop + 150, 900, 700), fitted);
    }

    [Fact]
    public void ShrinkingWindowKeepsItsPosition()
    {
        var work = new Rect(-1600, 0, 1600, 850);
        var window = new Rect(-1610, 500, 900, 700);

        var fitted = WindowPlacement.ResizeWithinWorkArea(window, new Size(900, 450), work);

        Assert.Equal(new Rect(-1610, 500, 900, 450), fitted);
    }

    [Fact]
    public void ReopeningMovesAnOffscreenWindowAndKeepsAPartiallyVisibleWindow()
    {
        var work = new Rect(0, 0, 1600, 850);
        var offscreen = new Rect(1600, 500, 900, 700);
        var visible = new Rect(1200, 500, 900, 700);

        Assert.Equal(new Rect(700, 150, 900, 700), WindowPlacement.RecoverOffscreen(offscreen, work));
        Assert.Equal(visible, WindowPlacement.RecoverOffscreen(visible, work));
    }
}
