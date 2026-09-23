using BossMonitor.Core;
internal static class Program
{
    private static int Main()
    {
        try {Maintenance.Cleanup();return 0;}catch{return 1;}
    }
}
