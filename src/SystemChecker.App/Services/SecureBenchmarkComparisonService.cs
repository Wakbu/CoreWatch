using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using SystemChecker.Models;

namespace SystemChecker.Services;

public sealed class LocalBenchmarkComparisonStore
{
    private readonly string _connectionString;
    public string DatabasePath { get; }

    public LocalBenchmarkComparisonStore(string? databasePath = null)
    {
        DatabasePath = databasePath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SystemChecker", "benchmark-comparison.db");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(DatabasePath))!);
        _connectionString = new SqliteConnectionStringBuilder { DataSource = DatabasePath, Pooling = false }.ToString();
    }

    public async Task InitializeAsync(CancellationToken token = default)
    {
        await using var db = new SqliteConnection(_connectionString); await db.OpenAsync(token);
        var command = db.CreateCommand(); command.CommandText = """
            CREATE TABLE IF NOT EXISTS local_benchmark_samples(
                measured_at TEXT PRIMARY KEY, benchmark_version TEXT NOT NULL, test_profile TEXT NOT NULL,
                cpu_model TEXT NOT NULL, gpu_model TEXT NOT NULL, memory_bucket_gb INTEGER NOT NULL,
                overall_score INTEGER NOT NULL CHECK(overall_score BETWEEN 0 AND 10000));
            CREATE INDEX IF NOT EXISTS ix_local_benchmark_compatibility ON local_benchmark_samples(benchmark_version,test_profile);
            """;
        await command.ExecuteNonQueryAsync(token);
    }

    public async Task SaveAsync(DateTimeOffset measuredAt, CommunityBenchmarkSample sample, CancellationToken token = default)
    {
        await InitializeAsync(token); await using var db = new SqliteConnection(_connectionString); await db.OpenAsync(token); var command = db.CreateCommand();
        command.CommandText = "INSERT OR IGNORE INTO local_benchmark_samples(measured_at,benchmark_version,test_profile,cpu_model,gpu_model,memory_bucket_gb,overall_score) VALUES($t,$v,$p,$c,$g,$m,$s)";
        command.Parameters.AddWithValue("$t", measuredAt.ToString("O")); command.Parameters.AddWithValue("$v", sample.BenchmarkVersion); command.Parameters.AddWithValue("$p", sample.TestProfile); command.Parameters.AddWithValue("$c", sample.CpuModel); command.Parameters.AddWithValue("$g", sample.GpuModel); command.Parameters.AddWithValue("$m", sample.MemoryCapacityBucketGb); command.Parameters.AddWithValue("$s", sample.OverallScore);
        await command.ExecuteNonQueryAsync(token);
    }

    public async Task<IReadOnlyList<CommunityBenchmarkSample>> GetCompatibleAsync(string benchmarkVersion, string testProfile, CancellationToken token = default)
    {
        await InitializeAsync(token); var samples = new List<CommunityBenchmarkSample>(); await using var db = new SqliteConnection(_connectionString); await db.OpenAsync(token); var command = db.CreateCommand();
        command.CommandText = "SELECT benchmark_version,test_profile,cpu_model,gpu_model,memory_bucket_gb,overall_score FROM local_benchmark_samples WHERE benchmark_version=$v AND test_profile=$p ORDER BY measured_at";
        command.Parameters.AddWithValue("$v", benchmarkVersion); command.Parameters.AddWithValue("$p", testProfile); await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token)) samples.Add(new(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetInt32(4), reader.GetInt32(5)));
        return samples;
    }
}

public sealed class ExternalBenchmarkReferenceClient : IDisposable
{
    public const int MaximumResponseBytes = 1024 * 1024;
    private readonly HttpClient _client;

    public ExternalBenchmarkReferenceClient(HttpMessageHandler? handler = null)
    {
        handler ??= new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false, UseDefaultCredentials = false };
        _client = new HttpClient(handler, true) { Timeout = TimeSpan.FromSeconds(6), MaxResponseContentBufferSize = MaximumResponseBytes };
        _client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("CoreWatch", "5.9.0"));
    }

    public async Task<ExternalBenchmarkComparisonResponse> GetAsync(Uri endpoint, CommunityBenchmarkSample subject, CancellationToken token = default)
    {
        ValidateEndpoint(endpoint);
        var query = $"benchmarkVersion={E(subject.BenchmarkVersion)}&testProfile={E(subject.TestProfile)}&cpu={E(subject.CpuModel)}&gpu={E(subject.GpuModel)}&memory={subject.MemoryCapacityBucketGb}&score={subject.OverallScore}";
        var builder = new UriBuilder(endpoint) { Query = query };
        using var request = new HttpRequestMessage(HttpMethod.Get, builder.Uri); request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
        if ((int)response.StatusCode is >= 300 and < 400) throw new HttpRequestException("외부 비교 서버 리디렉션은 보안을 위해 허용하지 않습니다.");
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is > MaximumResponseBytes) throw new InvalidDataException("외부 비교 응답이 허용 크기를 초과했습니다.");
        await using var stream = await response.Content.ReadAsStreamAsync(token); using var limited = await ReadLimitedAsync(stream, token); var result = await JsonSerializer.DeserializeAsync<ExternalBenchmarkComparisonResponse>(limited, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, token) ?? throw new InvalidDataException("외부 비교 응답이 비어 있습니다.");
        if (result.SchemaVersion != CommunityBenchmarkService.CurrentSchemaVersion || !Same(result.BenchmarkVersion, subject.BenchmarkVersion) || !Same(result.TestProfile, subject.TestProfile)) throw new InvalidDataException("호환되지 않는 외부 비교 응답입니다.");
        return result;
    }

    public static void ValidateEndpoint(Uri endpoint)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        if (!endpoint.IsAbsoluteUri || endpoint.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(endpoint.UserInfo)) throw new ArgumentException("외부 비교 주소는 계정 정보가 없는 HTTPS 주소만 허용합니다.", nameof(endpoint));
    }

    private static string E(string value) => Uri.EscapeDataString(value);
    private static async Task<MemoryStream> ReadLimitedAsync(Stream source, CancellationToken token)
    {
        var destination = new MemoryStream(); var buffer = new byte[8192];
        try
        {
            while (true)
            {
                var read = await source.ReadAsync(buffer, token); if (read == 0) break;
                if (destination.Length + read > MaximumResponseBytes) throw new InvalidDataException("외부 비교 응답이 허용 크기를 초과했습니다.");
                await destination.WriteAsync(buffer.AsMemory(0, read), token);
            }
            destination.Position = 0; return destination;
        }
        catch { destination.Dispose(); throw; }
    }

    private static bool Same(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    public void Dispose() => _client.Dispose();
}
