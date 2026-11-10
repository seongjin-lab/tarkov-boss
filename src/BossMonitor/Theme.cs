using System.Runtime.InteropServices;
using System.Windows.Data;
using System.Windows.Interop;
using Microsoft.Win32;

namespace BossMonitor;

internal static class Theme
{
    private const string PersonalizeKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const int ImmersiveDarkMode = 20;
    private static readonly (string Name, string Light, string Dark)[] Swatches =
    [
        (nameof(Bg), "#E8E4D9", "#171A17"),
        (nameof(Card), "#F7F4EB", "#232721"),
        (nameof(SurvivorCard), "#EFF5E0", "#303B2B"),
        (nameof(SurvivorOutline), "#557237", "#A7C37A"),
        (nameof(Text), "#22261F", "#EDE9D9"),
        (nameof(Muted), "#414336", "#C1C4B3"),
        (nameof(Line), "#ABA894", "#50584A"),
        (nameof(Green), "#3E512C", "#B9C997"),
        (nameof(Amber), "#754619", "#DDBD7E"),
        (nameof(Death), "#8A3529", "#F2AA93"),
        (nameof(Warning), "#F2DFD5", "#392B26"),
        (nameof(Soft), "#DEDCCD", "#2D3529"),
        (nameof(Hover), "#D6DAC6", "#394435"),
        (nameof(PortraitWell), "#E1DECF", "#30372B"),
    ];

    // 색 바인딩을 유지하면 WPF가 공유 브러시를 동결하지 않아 이미 그린 요소도 갱신된다.
    [ThreadStatic] private static Dictionary<string, (SolidColorBrush Brush, SolidColorBrush Source)>? brushes;
    [ThreadStatic] private static bool dark;
    private static Dictionary<string, (SolidColorBrush Brush, SolidColorBrush Source)> BrushesByName => brushes ??=
        Swatches.ToDictionary(swatch => swatch.Name, swatch => CreateBrush(swatch.Light));

    public static SolidColorBrush Bg => BrushesByName[nameof(Bg)].Brush;
    public static SolidColorBrush Card => BrushesByName[nameof(Card)].Brush;
    public static SolidColorBrush SurvivorCard => BrushesByName[nameof(SurvivorCard)].Brush;
    public static SolidColorBrush SurvivorOutline => BrushesByName[nameof(SurvivorOutline)].Brush;
    public static SolidColorBrush Text => BrushesByName[nameof(Text)].Brush;
    public static SolidColorBrush Muted => BrushesByName[nameof(Muted)].Brush;
    public static SolidColorBrush Line => BrushesByName[nameof(Line)].Brush;
    public static SolidColorBrush Green => BrushesByName[nameof(Green)].Brush;
    public static SolidColorBrush Amber => BrushesByName[nameof(Amber)].Brush;
    public static SolidColorBrush Death => BrushesByName[nameof(Death)].Brush;
    public static SolidColorBrush Warning => BrushesByName[nameof(Warning)].Brush;
    public static SolidColorBrush Soft => BrushesByName[nameof(Soft)].Brush;
    public static SolidColorBrush PortraitWell => BrushesByName[nameof(PortraitWell)].Brush;
    private const string Hover = "Hover";

    private static (SolidColorBrush Brush, SolidColorBrush Source) CreateBrush(string hex)
    {
        var source = new SolidColorBrush(Parse(hex));
        var brush = new SolidColorBrush();
        BindingOperations.SetBinding(brush, SolidColorBrush.ColorProperty,
            new Binding(nameof(SolidColorBrush.Color)) { Source = source, Mode = BindingMode.OneWay });
        return (brush, source);
    }

    public static void Apply(Application app)
    {
        foreach (var (name, swatch) in BrushesByName)
            app.Resources.Add("Theme" + name, swatch.Brush);
        app.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/BossMonitor;component/ThemeControls.xaml")
        });
    }

    public static string NormalizeMode(string? mode) => mode?.ToLowerInvariant() switch
    {
        "dark" => "dark",
        "light" => "light",
        _ => "system"
    };

    public static bool ApplyMode(string? mode)
    {
        bool next = NormalizeMode(mode) switch
        {
            "dark" => true,
            "light" => false,
            _ => Registry.GetValue(PersonalizeKey, "AppsUseLightTheme", 1) is 0
        };
        if (next == dark)
            return false;

        foreach (var swatch in Swatches)
            BrushesByName[swatch.Name].Source.Color = Parse(next ? swatch.Dark : swatch.Light);
        dark = next;
        return true;
    }

    public static void UpdateTitleBar(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero || !OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
            return;

        int enabled = dark ? 1 : 0;
        DwmSetWindowAttribute(handle, ImmersiveDarkMode, ref enabled, sizeof(int));
    }

    [DllImport("dwmapi.dll", ExactSpelling = true)]
    private static extern int DwmSetWindowAttribute(IntPtr handle, int attribute, ref int value, int size);

    private static Color Parse(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    public static Brush Color(string hex)
    {
        var brush = new SolidColorBrush(Parse(hex));
        brush.Freeze();
        return brush;
    }

    public static TextBlock Label(string text, double size = 14, Brush? color = null) => new()
    {
        Text = text, FontSize = Math.Max(size, 13), FontWeight = size >= 17 ? FontWeights.SemiBold : FontWeights.Normal,
        Foreground = color ?? Text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 8)
    };

    public static Button Action(string text, RoutedEventHandler click)
    {
        var button = new Button { Content = text };
        button.Click += click;
        return button;
    }

    public static BitmapImage Icon() => new(new Uri("pack://application:,,,/BossMonitor;component/Assets/radar.png"));

    public static void WindowIcon(Window window)
    {
        window.Icon = Icon();
        window.Background = Bg;
        window.Foreground = Text;
        window.FontFamily = new FontFamily(I18n.FontFamily);
        window.FontSize = 14;
        window.UseLayoutRounding = true;
        window.SourceInitialized += (sender, args) => UpdateTitleBar(window);
    }
}
