using SystemChecker.Models;

namespace SystemChecker.Services;

public sealed class DiagnosticsService
{
    public IReadOnlyList<DiagnosticItem> Evaluate(SystemSnapshot snapshot)
    {
        var results = new List<DiagnosticItem>();

        if (snapshot.MemoryUsagePercent >= 90)
            results.Add(new("위험", "메모리 사용량이 매우 높습니다", $"현재 {snapshot.MemoryUsagePercent:0}% 사용 중입니다."));
        else if (snapshot.MemoryUsagePercent >= 80)
            results.Add(new("주의", "메모리 여유가 적습니다", $"현재 {snapshot.MemoryUsagePercent:0}% 사용 중입니다."));

        if (snapshot.LowestDiskFreePercent < 10)
            results.Add(new("주의", "저장 공간이 부족한 드라이브가 있습니다", $"가장 부족한 드라이브의 여유 공간은 {snapshot.LowestDiskFreePercent:0.0}%입니다."));

        if (snapshot.CpuUsagePercent >= 95)
            results.Add(new("정보", "CPU 부하가 높습니다", $"현재 사용률은 {snapshot.CpuUsagePercent:0}%입니다. 지속 여부를 확인하세요."));

        if (results.Count == 0)
            results.Add(new("정상", "즉시 확인할 문제가 없습니다", "현재 관측 가능한 CPU, 메모리 및 저장 공간 지표가 정상 범위입니다."));

        return results;
    }
}
