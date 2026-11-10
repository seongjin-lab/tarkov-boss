using System.Windows.Media.Animation;

namespace BossMonitor;

internal sealed class WaitingRadar : Grid
{
    private const double Size = 72;
    // Match the center of the radar face in the shared icon.
    private static readonly Point Center = new(36, 34.75);
    private const double FaceRadius = 24.25;

    public WaitingRadar(bool animate)
    {
        Width = Height = Size;
        HorizontalAlignment = HorizontalAlignment.Left;
        IsHitTestVisible = false;
        Children.Add(new Image { Source = Theme.Icon() });
        Children.Add(DrawLayer(DrawFace));

        const double initialAngle = 45;
        var rotation = new RotateTransform(initialAngle, Center.X, Center.Y);
        var beam = DrawLayer(DrawBeam);
        beam.RenderTransform = rotation;
        Children.Add(beam);

        if (animate)
        {
            var sweep = new DoubleAnimation(initialAngle, initialAngle + 360, TimeSpan.FromSeconds(4))
            {
                RepeatBehavior = RepeatBehavior.Forever
            };
            IsVisibleChanged += (_, _) => rotation.BeginAnimation(
                RotateTransform.AngleProperty, IsVisible ? sweep : null);
        }
    }

    private static Image DrawLayer(Action<DrawingContext> draw)
    {
        var drawing = new DrawingGroup();
        using (var context = drawing.Open())
        {
            context.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, Size, Size));
            draw(context);
        }
        drawing.Freeze();
        return new Image { Source = new DrawingImage(drawing) };
    }

    private static void DrawFace(DrawingContext context)
    {
        // The bitmap already contains a beam. Replace its face while retaining the housing and rim.
        context.PushClip(new EllipseGeometry(Center, FaceRadius, FaceRadius));
        context.DrawEllipse(Theme.Color("#0B241A"), null, Center, FaceRadius, FaceRadius);
        var grid = new Pen(Theme.Color("#245137"), 0.4);
        for (int offset = -24; offset <= 24; offset += 6)
        {
            context.DrawLine(grid, new Point(Center.X + offset, 10), new Point(Center.X + offset, 59));
            context.DrawLine(grid, new Point(12, Center.Y + offset), new Point(60, Center.Y + offset));
        }
        context.DrawGeometry(Theme.Color("#091913"), null,
            Geometry.Parse("M 11,42 L 19,38 24,38 24,33 26,33 26,39 30,39 30,36 31,36 31,41 38,43 43,40 46,38 52,42 56,40 61,42 61,62 11,62 Z"));
        var reticle = new Pen(Theme.Color("#44CF71"), 1);
        context.DrawEllipse(null, reticle, Center, 20.5, 20.5);
        context.DrawGeometry(null, reticle, Geometry.Parse("M 36,12 L 36,17 M 36,52 L 36,57 M 13,34.75 L 18,34.75 M 54,34.75 L 59,34.75"));
        context.DrawEllipse(Theme.Color("#FFD358"), null, new Point(49, 28.5), 1.5, 1.5);
        context.DrawEllipse(Theme.Color("#53DF79"), null, Center, 1.2, 1.2);
        context.Pop();
    }

    private static void DrawBeam(DrawingContext context)
    {
        var trail = new LinearGradientBrush(Colors.Transparent, Color.FromArgb(140, 98, 255, 145), 0);
        context.DrawGeometry(trail, null, Geometry.Parse("M 36,34.75 L 22.1,14.9 A 24.25,24.25 0 0 1 36,10.5 Z"));
        context.DrawLine(new Pen(Theme.Color("#9BFFB4"), 0.85), Center, new Point(Center.X, Center.Y - FaceRadius));
    }
}
