using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;

namespace SystemChecker.Services;

internal sealed record ReleaseAsset(string Name, Uri DownloadUri, long Size, string Digest);
internal sealed record UpdateRelease(string Version, Uri ReleaseUri, string Notes, IReadOnlyList<ReleaseAsset> Assets, bool IsNewer);

internal sealed class AutomaticUpdateService
{
    private static readonly Uri LatestReleaseUri = new("https://api.github.com/repos/Wakbu/CoreWatch/releases/latest");
    private readonly HttpClient _client;

    public AutomaticUpdateService()
    {
        _client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("CoreWatch/6.1.1");
        _client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        _client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
    }

    public async Task<UpdateRelease> CheckAsync(CancellationToken token = default)
    {
        using var response = await _client.GetAsync(LatestReleaseUri, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(token);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: token);
        var root = document.RootElement;
        var tag = root.GetProperty("tag_name").GetString()?.Trim() ?? throw new InvalidDataException("릴리스 버전 정보가 없습니다.");
        var releaseUri = RequireHttpsUri(root.GetProperty("html_url").GetString());
        var notes = root.TryGetProperty("body", out var body) ? body.GetString() ?? string.Empty : string.Empty;
        var assets = new List<ReleaseAsset>();
        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString() ?? string.Empty;
            if (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && !name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) continue;
            var digest = asset.TryGetProperty("digest", out var digestNode) ? digestNode.GetString() ?? string.Empty : string.Empty;
            assets.Add(new ReleaseAsset(name, RequireHttpsUri(asset.GetProperty("browser_download_url").GetString()), asset.GetProperty("size").GetInt64(), digest));
        }
        return new UpdateRelease(tag.TrimStart('v', 'V'), releaseUri, notes, assets, IsNewerVersion(CurrentVersion, tag));
    }

    public async Task<string> DownloadVerifiedAsync(UpdateRelease release, ReleaseAsset asset, IProgress<double>? progress = null, CancellationToken token = default)
    {
        if (!release.Assets.Contains(asset)) throw new InvalidOperationException("확인된 릴리스 자산이 아닙니다.");
        var expectedHash = ParseSha256Digest(asset.Digest);
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CoreWatch", "Updates", $"v{release.Version}");
        Directory.CreateDirectory(directory);
        var finalPath = Path.Combine(directory, Path.GetFileName(asset.Name));
        var partialPath = finalPath + ".partial";
        try
        {
            using var response = await _client.GetAsync(asset.DownloadUri, HttpCompletionOption.ResponseHeadersRead, token);
            response.EnsureSuccessStatusCode();
            await using var source = await response.Content.ReadAsStreamAsync(token);
            await using var target = new FileStream(partialPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
            using var sha = SHA256.Create();
            var buffer = new byte[81920];
            long received = 0;
            int count;
            while ((count = await source.ReadAsync(buffer, token)) > 0)
            {
                await target.WriteAsync(buffer.AsMemory(0, count), token);
                sha.TransformBlock(buffer, 0, count, null, 0);
                received += count;
                progress?.Report(asset.Size > 0 ? Math.Min(1, (double)received / asset.Size) : 0);
            }
            sha.TransformFinalBlock([], 0, 0);
            var actual = Convert.ToHexString(sha.Hash!);
            if (!actual.Equals(expectedHash, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("다운로드한 파일의 SHA-256 값이 GitHub 릴리스와 일치하지 않습니다.");
            target.Close();
            File.Move(partialPath, finalPath, true);
            progress?.Report(1);
            return finalPath;
        }
        catch
        {
            try { if (File.Exists(partialPath)) File.Delete(partialPath); } catch { }
            throw;
        }
    }

    internal static string CurrentVersion => Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.0.0";
    internal static bool IsNewerVersion(string current, string candidate) => ParseVersion(candidate) > ParseVersion(current);
    internal static string ParseSha256Digest(string digest)
    {
        const string prefix = "sha256:";
        if (!digest.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("릴리스 자산에 SHA-256 검증값이 없습니다.");
        var value = digest[prefix.Length..];
        if (value.Length != 64 || value.Any(ch => !Uri.IsHexDigit(ch))) throw new InvalidDataException("릴리스 SHA-256 검증값 형식이 올바르지 않습니다.");
        return value;
    }
    private static Version ParseVersion(string value)
    {
        var clean = value.Trim().TrimStart('v', 'V').Split('-', '+')[0];
        return Version.TryParse(clean, out var parsed) ? parsed : throw new InvalidDataException($"버전 형식이 올바르지 않습니다: {value}");
    }
    private static Uri RequireHttpsUri(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps) throw new InvalidDataException("안전한 HTTPS 주소가 아닙니다.");
        return uri;
    }
}
