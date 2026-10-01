using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;

namespace BossMonitor.Core;

public record UpdateInfo(Version Version,string Tag);

public static class UpdateChecker
{
    public const string ReleasePage="https://github.com/seongjin-lab/tarkov-boss/releases/latest";
    private const string LatestReleaseApi="https://api.github.com/repos/seongjin-lab/tarkov-boss/releases/latest";
    private static readonly HttpClient Client=CreateClient();

    public static Version CurrentVersion
        => Assembly.GetEntryAssembly()?.GetName().Version ?? Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0,0,0);

    public static async Task<UpdateInfo?> CheckAsync(CancellationToken cancellationToken=default)
    {
        using var request=new HttpRequestMessage(HttpMethod.Get,LatestReleaseApi);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Add("X-GitHub-Api-Version","2022-11-28");
        using var response=await Client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,cancellationToken);
        if(!response.IsSuccessStatusCode)return null;
        await using var stream=await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json=await JsonDocument.ParseAsync(stream,cancellationToken:cancellationToken);
        if(!json.RootElement.TryGetProperty("tag_name",out var tagElement))return null;
        string tag=tagElement.GetString()?.Trim()??"";
        if(!Version.TryParse(tag.TrimStart('v','V'),out var latest) || latest<=CurrentVersion)return null;
        return new(latest,tag);
    }

    private static HttpClient CreateClient()
    {
        var client=new HttpClient {Timeout=TimeSpan.FromSeconds(6)};
        client.DefaultRequestHeaders.UserAgent.ParseAdd("TarkovBossMonitor/"+CurrentVersion.ToString(3));
        return client;
    }
}
