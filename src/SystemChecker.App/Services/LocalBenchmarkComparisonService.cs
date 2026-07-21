using Microsoft.Data.Sqlite;
using SystemChecker.Models;

namespace SystemChecker.Services;

public static class LocalBenchmarkComparisonService
{
    public static LocalBenchmarkComparison Compare(int score, IEnumerable<int> referenceScores)
    {
        var scores = referenceScores.Where(value => value >= 0).Order().ToArray();
        if (scores.Length == 0) return new(0, 0, 0, "기록 없음");
        var lower = scores.Count(value => value < score); var equal = scores.Count(value => value == score);
        var percentile = Math.Round(100d * (lower + equal * .5d) / scores.Length, 1); var middle = scores.Length / 2;
        var median = scores.Length % 2 == 0 ? ((double)scores[middle - 1] + scores[middle]) / 2d : scores[middle];
        return new(scores.Length, percentile, median, "로컬 비교 가능");
    }
}

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

    public async Task SaveAsync(DateTimeOffset measuredAt, LocalBenchmarkSample sample, CancellationToken token = default)
    {
        await using var db = new SqliteConnection(_connectionString); await db.OpenAsync(token); var command = db.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS local_benchmark_samples(measured_at TEXT PRIMARY KEY,benchmark_version TEXT NOT NULL,test_profile TEXT NOT NULL,overall_score INTEGER NOT NULL CHECK(overall_score BETWEEN 0 AND 10000));
            CREATE INDEX IF NOT EXISTS ix_local_benchmark_compatibility ON local_benchmark_samples(benchmark_version,test_profile);
            INSERT OR IGNORE INTO local_benchmark_samples(measured_at,benchmark_version,test_profile,overall_score) VALUES($t,$v,$p,$s);
            """;
        command.Parameters.AddWithValue("$t", measuredAt.ToString("O")); command.Parameters.AddWithValue("$v", sample.BenchmarkVersion); command.Parameters.AddWithValue("$p", sample.TestProfile); command.Parameters.AddWithValue("$s", sample.OverallScore); await command.ExecuteNonQueryAsync(token);
    }

    public async Task<IReadOnlyList<int>> GetScoresAsync(string benchmarkVersion, string testProfile, CancellationToken token = default)
    {
        var scores = new List<int>(); await using var db = new SqliteConnection(_connectionString); await db.OpenAsync(token); var command = db.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS local_benchmark_samples(measured_at TEXT PRIMARY KEY,benchmark_version TEXT NOT NULL,test_profile TEXT NOT NULL,overall_score INTEGER NOT NULL CHECK(overall_score BETWEEN 0 AND 10000));
            SELECT overall_score FROM local_benchmark_samples WHERE benchmark_version=$v AND test_profile=$p ORDER BY measured_at;
            """;
        command.Parameters.AddWithValue("$v", benchmarkVersion); command.Parameters.AddWithValue("$p", testProfile); await using var reader = await command.ExecuteReaderAsync(token); while (await reader.ReadAsync(token)) scores.Add(reader.GetInt32(0)); return scores;
    }
}
