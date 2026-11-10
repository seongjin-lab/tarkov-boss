namespace BossMonitor;

internal static class Portraits
{
    private const string Fallback = "custom-boss";
    private static readonly string[] AssetRoles =
    [
        "bossKilla", "bossTagilla", "bossBully", "bossKojaniy", "bossGluhar", "bossSanitar",
        "bossKnight", "followerBigPipe", "followerBirdEye", "bossZryachiy", "bossBoar", "bossKolontay",
        "bossPartisan", "sectantPriest", "bossScourgeEventLab", "infectedScreamer", "infectedTagilla",
        "ai-pmc", "scav", "exUsec", "pmcBot", "btr", Fallback
    ];
    private static readonly Dictionary<string, BitmapImage> Images =
        AssetRoles.ToDictionary(role => role, Load, StringComparer.OrdinalIgnoreCase);

    public static ImageSource ForRole(string role) => Images.GetValueOrDefault(role) ?? Images[Fallback];

    private static BitmapImage Load(string role)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.UriSource = new Uri($"pack://application:,,,/BossMonitor;component/Assets/Portraits/{role}.png");
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
