namespace SystemChecker.Services;

internal sealed record RecommendationInput(string Purpose, int LogicalProcessors, double MemoryGb, double SystemDriveFreePercent, int NetworkWarnings, int StorageWarnings);
internal sealed record PersonalizedRecommendation(int Score, string Grade, IReadOnlyList<string> Items, string Summary);

internal static class PersonalizedRecommendationService
{
    internal static PersonalizedRecommendation Calculate(RecommendationInput input)
    {
        var cpuTarget = input.Purpose is "게임" or "콘텐츠 제작" ? 12d : 8d;
        var memoryTarget = input.Purpose == "콘텐츠 제작" ? 32d : input.Purpose == "게임" ? 16d : 12d;
        var score = Math.Clamp((int)Math.Round(Math.Min(1, input.LogicalProcessors / cpuTarget) * 30 + Math.Min(1, input.MemoryGb / memoryTarget) * 30 + Math.Min(1, input.SystemDriveFreePercent / 20) * 25 + 15 - input.NetworkWarnings * 5 - input.StorageWarnings * 7), 0, 100);
        var items = new List<string>();
        if (input.LogicalProcessors < cpuTarget) items.Add($"{input.Purpose} 용도에서는 논리 프로세서 {cpuTarget:0}개 이상이 유리합니다.");
        if (input.MemoryGb < memoryTarget) items.Add($"메모리를 {memoryTarget:0}GB 이상으로 확보하면 {input.Purpose} 작업의 여유가 커집니다.");
        if (input.SystemDriveFreePercent < 15) items.Add("시스템 드라이브 여유 공간을 15% 이상 확보하세요.");
        if (input.NetworkWarnings > 0) items.Add("네트워크 진단의 지연·손실·어댑터 오류 항목을 먼저 확인하세요.");
        if (input.StorageWarnings > 0) items.Add("저장장치 진단에서 확인이 필요한 볼륨을 점검하세요.");
        if (items.Count == 0) items.Add("현재 구성은 선택한 용도에 균형 있게 맞습니다. 정기 진단만 유지하세요.");
        var grade = score >= 85 ? "매우 적합" : score >= 70 ? "적합" : score >= 50 ? "보완 권장" : "업그레이드 검토";
        return new PersonalizedRecommendation(score, grade, items.Take(4).ToList(), $"{input.Purpose} 기준 {score}점 · {grade}");
    }
}
