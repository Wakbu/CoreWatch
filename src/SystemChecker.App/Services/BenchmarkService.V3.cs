using System.Diagnostics;
using System.Security.Cryptography;
using SystemChecker.Models;

namespace SystemChecker.Services;

public sealed class BenchmarkService
{
    public Task<BenchmarkResult> RunAsync(IProgress<string>? progress, CancellationToken cancellationToken) =>
        Task.Run(() => Run(progress, cancellationToken), cancellationToken);

    private static BenchmarkResult Run(IProgress<string>? progress, CancellationToken cancellationToken)
    {
        var buffer = new byte[1024 * 1024];
        RandomNumberGenerator.Fill(buffer);
        progress?.Report("워밍업 · CPU");
        for (var i = 0; i < 8; i++) _ = SHA256.HashData(buffer);

        progress?.Report("측정 중 · CPU SHA-256");
        long operations = 0;
        var cpuWatch = Stopwatch.StartNew();
        while (cpuWatch.Elapsed < TimeSpan.FromSeconds(3))
        {
            cancellationToken.ThrowIfCancellationRequested();
            _ = SHA256.HashData(buffer); operations++;
        }
        var cpuMbps = operations / cpuWatch.Elapsed.TotalSeconds;

        progress?.Report("측정 중 · 메모리 대역폭");
        var source = new byte[64 * 1024 * 1024];
        var destination = new byte[source.Length];
        Buffer.BlockCopy(source, 0, destination, 0, source.Length);
        long copied = 0;
        var memoryWatch = Stopwatch.StartNew();
        while (memoryWatch.Elapsed < TimeSpan.FromSeconds(2.5))
        {
            cancellationToken.ThrowIfCancellationRequested();
            Buffer.BlockCopy(source, 0, destination, 0, source.Length); copied += source.Length;
        }
        var memoryGbps = copied / memoryWatch.Elapsed.TotalSeconds / 1024d / 1024d / 1024d;
        var cpuScore = (int)Math.Round(cpuMbps);
        var memoryScore = (int)Math.Round(memoryGbps / 20d * 1000d);
        var overall = (int)Math.Round(cpuScore * .6 + memoryScore * .4);
        var grade = overall switch { >= 1500 => "S", >= 1100 => "A", >= 800 => "B", >= 500 => "C", _ => "D" };
        progress?.Report("완료");
        return new(DateTimeOffset.Now, cpuMbps, memoryGbps, cpuScore, memoryScore, overall, grade,
            $"{Environment.OSVersion.VersionString}; {Environment.ProcessorCount} logical processors; .NET {Environment.Version}",
            "CPU 1점 = SHA-256 1MiB/s, Memory 1000점 = 20GB/s, Overall = CPU 60% + Memory 40%");
    }
}
