using System.Text.Json;
using System.Text.Json.Serialization;
using SystemChecker.Models;

namespace SystemChecker.Services;

public static class CommunityBenchmarkService
{
    public const int CurrentSchemaVersion = 1;
    public const int MinimumSampleCount = 10;
    private static readonly JsonSerializerOptions PreviewJsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
    private static readonly string[] ExcludedPersonalData = ["사용자·Windows 계정 이름", "컴퓨터 이름", "IP 주소", "파일 경로", "프로세스 목록", "하드웨어 일련번호"];

    public static CommunityBenchmarkSubmission CreateSubmission(string appVersion, string benchmarkVersion, string testProfile, FullBenchmarkResult result, CommunityHardwareProfile hardware, CommunityBenchmarkConditions conditions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appVersion); ArgumentException.ThrowIfNullOrWhiteSpace(benchmarkVersion); ArgumentException.ThrowIfNullOrWhiteSpace(testProfile); ArgumentNullException.ThrowIfNull(result); ArgumentNullException.ThrowIfNull(hardware); ArgumentNullException.ThrowIfNull(conditions);
        return new CommunityBenchmarkSubmission(CurrentSchemaVersion, appVersion.Trim(), benchmarkVersion.Trim(), testProfile.Trim(), result.MeasuredAt, hardware,
            new CommunityBenchmarkMeasurements(result.CpuMemory.CpuThroughputMbps, result.CpuMemory.MemoryBandwidthGbps, result.Disk.SequentialReadMbps, result.Disk.SequentialWriteMbps, result.Disk.RandomReadIops, result.Disk.RandomWriteIops, result.Gpu.FramesPerSecond, result.Gpu.MegaPixelsPerSecond, result.CpuMemory.CpuScore, result.CpuMemory.MemoryScore, result.Disk.Score, result.Gpu.Score, result.OverallScore), conditions);
    }

    public static AnonymousSubmissionPreview CreateAnonymousPreview(string installationId, CommunityBenchmarkSubmission submission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(installationId); ArgumentNullException.ThrowIfNull(submission);
        return new AnonymousSubmissionPreview(JsonSerializer.Serialize(new CommunityBenchmarkEnvelope(installationId.Trim(), submission), PreviewJsonOptions), ExcludedPersonalData);
    }

    public static CommunityBenchmarkComparison Compare(CommunityBenchmarkSample subject, IEnumerable<CommunityBenchmarkSample> referenceSamples, int minimumSampleCount = MinimumSampleCount)
    {
        ArgumentNullException.ThrowIfNull(subject); ArgumentNullException.ThrowIfNull(referenceSamples); if (minimumSampleCount < 1) throw new ArgumentOutOfRangeException(nameof(minimumSampleCount));
        var compatible = referenceSamples.Where(sample => Same(sample.BenchmarkVersion, subject.BenchmarkVersion) && Same(sample.TestProfile, subject.TestProfile) && sample.OverallScore >= 0).ToArray();
        return new CommunityBenchmarkComparison(Calculate(subject.OverallScore, compatible, minimumSampleCount), Calculate(subject.OverallScore, compatible.Where(x => Same(x.CpuModel, subject.CpuModel)), minimumSampleCount), Calculate(subject.OverallScore, compatible.Where(x => Same(x.GpuModel, subject.GpuModel)), minimumSampleCount), Calculate(subject.OverallScore, compatible.Where(x => x.MemoryCapacityBucketGb == subject.MemoryCapacityBucketGb), minimumSampleCount));
    }

    private static BenchmarkPercentile Calculate(int score, IEnumerable<CommunityBenchmarkSample> samples, int minimumSampleCount)
    {
        var scores = samples.Select(x => x.OverallScore).Order().ToArray();
        if (scores.Length < minimumSampleCount) return new BenchmarkPercentile(scores.Length, null, null, "표본 부족");
        var lower = scores.Count(x => x < score); var equal = scores.Count(x => x == score); var percentile = 100d * (lower + equal * 0.5d) / scores.Length; var middle = scores.Length / 2; var median = scores.Length % 2 == 0 ? ((double)scores[middle - 1] + scores[middle]) / 2d : scores[middle];
        return new BenchmarkPercentile(scores.Length, Math.Round(percentile, 1), median, "비교 가능");
    }

    private static bool Same(string left, string right) => string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
}
