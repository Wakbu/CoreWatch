using Microsoft.Data.Sqlite;
using System.Text.Json;
using SystemChecker.Models;

namespace SystemChecker.Services;

public sealed class HistoryRepository
{
    private readonly string _connectionString;
    public string DatabasePath { get; }
    public HistoryRepository()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SystemChecker");
        Directory.CreateDirectory(folder); DatabasePath = Path.Combine(folder, "history.db");
        _connectionString = new SqliteConnectionStringBuilder { DataSource = DatabasePath }.ToString();
    }
    public async Task InitializeAsync(CancellationToken token = default)
    {
        await using var db = new SqliteConnection(_connectionString); await db.OpenAsync(token);
        var command = db.CreateCommand(); command.CommandText = """
            CREATE TABLE IF NOT EXISTS telemetry(id INTEGER PRIMARY KEY, captured_at TEXT NOT NULL, cpu REAL, memory REAL, disk REAL, network REAL, cpu_temp REAL, gpu_temp REAL);
            CREATE TABLE IF NOT EXISTS benchmarks(id INTEGER PRIMARY KEY, measured_at TEXT NOT NULL, score INTEGER, grade TEXT, cpu INTEGER, memory INTEGER, disk INTEGER, gpu INTEGER, payload TEXT NOT NULL);
            CREATE INDEX IF NOT EXISTS ix_telemetry_time ON telemetry(captured_at); CREATE INDEX IF NOT EXISTS ix_benchmarks_time ON benchmarks(measured_at);
            """; await command.ExecuteNonQueryAsync(token);
    }
    public async Task AddTelemetryAsync(SystemSnapshot s, SensorSummary sensors, CancellationToken token = default)
    {
        await using var db = new SqliteConnection(_connectionString); await db.OpenAsync(token); var c = db.CreateCommand();
        c.CommandText = "INSERT INTO telemetry(captured_at,cpu,memory,disk,network,cpu_temp,gpu_temp) VALUES($t,$c,$m,$d,$n,$ct,$gt); DELETE FROM telemetry WHERE captured_at < $cutoff;";
        c.Parameters.AddWithValue("$t", s.CapturedAt.ToString("O")); c.Parameters.AddWithValue("$c", s.CpuUsagePercent); c.Parameters.AddWithValue("$m", s.MemoryUsagePercent); c.Parameters.AddWithValue("$d", s.DiskReadMbps+s.DiskWriteMbps); c.Parameters.AddWithValue("$n", s.NetworkReceiveMbps+s.NetworkSendMbps); c.Parameters.AddWithValue("$ct", (object?)sensors.CpuTemperature ?? DBNull.Value); c.Parameters.AddWithValue("$gt", (object?)sensors.GpuTemperature ?? DBNull.Value); c.Parameters.AddWithValue("$cutoff", DateTimeOffset.Now.AddDays(-30).ToString("O")); await c.ExecuteNonQueryAsync(token);
    }
    public async Task AddBenchmarkAsync(FullBenchmarkResult b, CancellationToken token = default)
    {
        await using var db = new SqliteConnection(_connectionString); await db.OpenAsync(token); var c=db.CreateCommand();
        c.CommandText="INSERT INTO benchmarks(measured_at,score,grade,cpu,memory,disk,gpu,payload) VALUES($t,$s,$g,$c,$m,$d,$p,$j)";
        c.Parameters.AddWithValue("$t",b.MeasuredAt.ToString("O"));c.Parameters.AddWithValue("$s",b.OverallScore);c.Parameters.AddWithValue("$g",b.Grade);c.Parameters.AddWithValue("$c",b.CpuMemory.CpuScore);c.Parameters.AddWithValue("$m",b.CpuMemory.MemoryScore);c.Parameters.AddWithValue("$d",b.Disk.Score);c.Parameters.AddWithValue("$p",b.Gpu.Score);c.Parameters.AddWithValue("$j",JsonSerializer.Serialize(b));await c.ExecuteNonQueryAsync(token);
    }
    public async Task<IReadOnlyList<BenchmarkHistoryItem>> GetBenchmarksAsync(int limit=20, CancellationToken token=default)
    {
        var list=new List<BenchmarkHistoryItem>(); await using var db=new SqliteConnection(_connectionString);await db.OpenAsync(token);var c=db.CreateCommand();c.CommandText="SELECT id,measured_at,score,grade,cpu,memory,disk,gpu FROM benchmarks ORDER BY id DESC LIMIT $l";c.Parameters.AddWithValue("$l",limit);await using var r=await c.ExecuteReaderAsync(token);int? newest=null;
        while(await r.ReadAsync(token)){var score=r.GetInt32(2);newest??=score;list.Add(new(r.GetInt64(0),DateTimeOffset.Parse(r.GetString(1)),score,r.GetString(3),r.GetInt32(4),r.GetInt32(5),r.GetInt32(6),r.GetInt32(7),list.Count==0?"LATEST":$"{score-newest:+#;-#;0}"));}return list;
    }
    public async Task<int?> GetLatestScoreAsync(CancellationToken token=default){await using var db=new SqliteConnection(_connectionString);await db.OpenAsync(token);var c=db.CreateCommand();c.CommandText="SELECT score FROM benchmarks ORDER BY id DESC LIMIT 1";var v=await c.ExecuteScalarAsync(token);return v is long l?(int)l:v as int?;}
}

