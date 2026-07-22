using System.Net.NetworkInformation;

namespace SystemChecker.Services;

internal sealed record NetworkAdapterDiagnostic(string Name, string Speed, long ReceiveErrors, long SendErrors, long Discards, string Assessment);
internal sealed record LatencyDiagnostic(string Target, int Sent, int Received, double LossPercent, double AverageMs, string Assessment);
internal sealed record NetworkDiagnosticsResult(IReadOnlyList<NetworkAdapterDiagnostic> Adapters, IReadOnlyList<LatencyDiagnostic> Latencies, string Summary);

internal sealed class NetworkDiagnosticsService
{
    public async Task<NetworkDiagnosticsResult> CaptureAsync(string? externalTarget, CancellationToken token = default)
    {
        var active = NetworkInterface.GetAllNetworkInterfaces().Where(item => item.OperationalStatus == OperationalStatus.Up && item.NetworkInterfaceType is not NetworkInterfaceType.Loopback and not NetworkInterfaceType.Tunnel).ToList();
        var adapters = active.Select(CreateAdapterDiagnostic).ToList();
        var targets = active.SelectMany(item => item.GetIPProperties().GatewayAddresses).Select(item => item.Address.ToString()).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct().Take(1).ToList();
        if (!string.IsNullOrWhiteSpace(externalTarget)) targets.Add(ValidateTarget(externalTarget));
        var latencies = new List<LatencyDiagnostic>();
        foreach (var target in targets.Distinct(StringComparer.OrdinalIgnoreCase)) latencies.Add(await MeasureAsync(target, token));
        var warning = adapters.Count(item => item.Assessment != "정상") + latencies.Count(item => item.Assessment != "정상");
        return new NetworkDiagnosticsResult(adapters, latencies, adapters.Count == 0 ? "활성 네트워크 어댑터가 없습니다." : warning == 0 ? $"어댑터 {adapters.Count}개 · 지연 및 오류 정상" : $"확인이 필요한 네트워크 항목 {warning}개");
    }

    internal static string ValidateTarget(string value)
    {
        var target = value.Trim();
        if (target.Length is < 1 or > 253 || target.Any(ch => !(char.IsLetterOrDigit(ch) || ch is '.' or '-' or ':'))) throw new ArgumentException("진단 대상은 IP 주소 또는 호스트 이름만 입력하세요.");
        return target;
    }

    private static NetworkAdapterDiagnostic CreateAdapterDiagnostic(NetworkInterface adapter)
    {
        try
        {
            var stats = adapter.GetIPv4Statistics();
            var errors = stats.IncomingPacketsWithErrors + stats.OutgoingPacketsWithErrors;
            var discards = stats.IncomingPacketsDiscarded + stats.OutgoingPacketsDiscarded;
            var assessment = errors > 0 || discards > 100 ? "오류 확인" : "정상";
            return new NetworkAdapterDiagnostic(adapter.Name, $"{adapter.Speed / 1_000_000d:0.#} Mbps", stats.IncomingPacketsWithErrors, stats.OutgoingPacketsWithErrors, discards, assessment);
        }
        catch { return new NetworkAdapterDiagnostic(adapter.Name, "확인 불가", 0, 0, 0, "통계 확인 불가"); }
    }

    private static async Task<LatencyDiagnostic> MeasureAsync(string target, CancellationToken token)
    {
        const int sent = 4;
        var times = new List<long>();
        using var ping = new Ping();
        for (var i = 0; i < sent; i++)
        {
            token.ThrowIfCancellationRequested();
            try { var reply = await ping.SendPingAsync(target, 1200); if (reply.Status == IPStatus.Success) times.Add(reply.RoundtripTime); }
            catch (PingException) { }
        }
        var loss = (sent - times.Count) * 100d / sent;
        var average = times.Count == 0 ? 0 : times.Average();
        var assessment = loss >= 25 ? "패킷 손실" : average > 100 ? "지연 높음" : "정상";
        return new LatencyDiagnostic(target, sent, times.Count, loss, average, assessment);
    }
}
