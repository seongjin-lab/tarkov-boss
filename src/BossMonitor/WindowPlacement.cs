using System.Windows.Interop;

namespace BossMonitor;

internal static class WindowPlacement
{
    internal static bool TryGetWorkArea(Window window, out Rect workArea)
    {
        if (PresentationSource.FromVisual(window) is HwndSource { CompositionTarget: { } target } source)
        {
            var screen = Forms.Screen.FromHandle(source.Handle).WorkingArea;
            // Screen coordinates and WPF window coordinates use different units.
            workArea = new Rect(screen.X, screen.Y, screen.Width, screen.Height);
            workArea.Transform(target.TransformFromDevice);
            return true;
        }

        workArea = Rect.Empty;
        return false;
    }

    internal static Rect ResizeWithinWorkArea(Rect bounds, Size size, Rect workArea)
    {
        var fitted = FitToWorkArea(new Rect(bounds.Location, size), workArea);
        // Leave the user's position alone when shrinking or leaving an axis unchanged.
        return new Rect(size.Width > bounds.Width ? fitted.X : bounds.X,
            size.Height > bounds.Height ? fitted.Y : bounds.Y, size.Width, size.Height);
    }

    private static Rect FitToWorkArea(Rect bounds, Rect workArea)
    {
        bounds.X = Math.Clamp(bounds.X, workArea.Left, Math.Max(workArea.Left, workArea.Right - bounds.Width));
        bounds.Y = Math.Clamp(bounds.Y, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - bounds.Height));
        return bounds;
    }

    internal static Rect RecoverOffscreen(Rect bounds, Rect workArea)
    {
        var visible = Rect.Intersect(bounds, workArea);
        return visible.Width > 0 && visible.Height > 0 ? bounds : FitToWorkArea(bounds, workArea);
    }
}
