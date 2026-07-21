using Microsoft.Data.Sqlite;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SystemChecker.Models;
using SystemChecker.Services;

namespace SystemChecker;

public partial class TelemetryMainWindow
{
    private const string BenchmarkVersion = "corewatch-full-v1";
    private const string BenchmarkProfile = "standard-v1";
    private readonly LocalBenchmarkComparisonStore _localComparisonStore = new();
    private readonly ExternalBenchmarkReferenceClient _externalReferenceClient = new();
    private TextBlock? _localComparisonStatus, _externalComparisonStatus;
    private CheckBox? _externalComparisonConsent;
    private TextBox? _externalEndpoint;

    private void InitializeSecureBenchmarkComparison()
    {
        if (BenchmarkPage.Content is not StackPanel root) return;
        var panel = Panel(); panel.Margin = new Thickness(0, 12, 0, 0); var stack = new StackPanel(); panel.Child = stack;
        stack.Children.Add(Heading("SECURE COMMUNITY / OFFLINE COMPARISON"));
        stack.Children.Add(new TextBlock { Text = "기본값은 로컬 전용입니다. 외부 조회는 명시적으로 실행할 때만 HTTPS GET을 사용합니다. 계정·컴퓨터 이름·파일·프로세스·일련번호는 보내지 않지만 모든 인터넷 연결처럼 서버는 접속 IP를 볼 수 있습니다.", Foreground = new SolidColorBrush(Color.FromRgb(104, 112, 125)), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 7, 0, 12) });
        _localComparisonStatus = new TextBlock { Text = "로컬 비교: 벤치마크 실행 후 동일 버전의 내 기록만 비교합니다.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) }; stack.Children.Add(_localComparisonStatus);
        var localButton = Action("내 기록으로 비교", LocalComparison_Click); stack.Children.Add(localButton);
        _externalComparisonConsent = new CheckBox { Content = "외부 비교를 이번 실행에서만 허용", IsChecked = false, Margin = new Thickness(0, 14, 0, 8) }; stack.Children.Add(_externalComparisonConsent);
        _externalEndpoint = new TextBox { Text = "", ToolTip = "예: https://example.com/v1/benchmarks/reference", Height = 34, Padding = new Thickness(9, 6, 9, 6) }; stack.Children.Add(_externalEndpoint);
        stack.Children.Add(new TextBlock { Text = "주소는 저장하지 않습니다. HTTP·리디렉션·쿠키·Windows 인증은 차단하며 응답은 1 MiB로 제한합니다.", Foreground = new SolidColorBrush(Color.FromRgb(104, 112, 125)), FontSize = 10, Margin = new Thickness(0, 6, 0, 10) });
        stack.Children.Add(Action("외부 기준 조회", ExternalComparison_Click, "#315D8A", "#FFFFFF"));
        _externalComparisonStatus = new TextBlock { Text = "외부 비교: 꺼짐", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0) }; stack.Children.Add(_externalComparisonStatus);
        root.Children.Add(panel);
    }

    private async void LocalComparison_Click(object sender, RoutedEventArgs e) => await RefreshSecureComparisonAsync(false, _monitorCancellation.Token);

    private async void ExternalComparison_Click(object sender, RoutedEventArgs e)
    {
        if (_externalComparisonConsent?.IsChecked != true) { if (_externalComparisonStatus is not null) _externalComparisonStatus.Text = "외부 비교: 사용자가 명시적으로 허용하지 않아 실행하지 않았습니다."; return; }
        if (!Uri.TryCreate(_externalEndpoint?.Text?.Trim(), UriKind.Absolute, out var endpoint)) { if (_externalComparisonStatus is not null) _externalComparisonStatus.Text = "외부 비교: 유효한 HTTPS 주소를 입력하세요."; return; }
        var subject = CurrentComparisonSample(); if (subject is null) { if (_externalComparisonStatus is not null) _externalComparisonStatus.Text = "외부 비교: 먼저 벤치마크를 실행하세요."; return; }
        try { if (_externalComparisonStatus is not null) _externalComparisonStatus.Text = "외부 비교: HTTPS 기준 데이터를 조회하는 중…"; var result = await _externalReferenceClient.GetAsync(endpoint, subject, _monitorCancellation.Token); if (_externalComparisonStatus is not null) _externalComparisonStatus.Text = "외부 비교: " + FormatComparison(result.Comparison); }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidDataException or ArgumentException) { if (_externalComparisonStatus is not null) _externalComparisonStatus.Text = $"외부 비교 불가 · 로컬 비교 유지: {ex.Message}"; await RefreshSecureComparisonAsync(false, _monitorCancellation.Token); }
    }

    private async Task RefreshSecureComparisonAsync(bool saveCurrent, CancellationToken token)
    {
        var subject = CurrentComparisonSample(); if (subject is null) { if (_localComparisonStatus is not null) _localComparisonStatus.Text = "로컬 비교: 먼저 벤치마크를 실행하세요."; return; }
        try { if (saveCurrent && _viewModel.LastBenchmark is { } result) await _localComparisonStore.SaveAsync(result.MeasuredAt, subject, token); var samples = await _localComparisonStore.GetCompatibleAsync(subject.BenchmarkVersion, subject.TestProfile, token); var comparison = CommunityBenchmarkService.Compare(subject, samples, 1); if (_localComparisonStatus is not null) _localComparisonStatus.Text = $"로컬 전용 · 내 기록 {samples.Count}개: {FormatPercentile(comparison.Overall)}"; }
        catch (Exception ex) when (ex is SqliteException or IOException) { if (_localComparisonStatus is not null) _localComparisonStatus.Text = $"로컬 비교 저장소 오류: {ex.Message}"; }
    }

    private CommunityBenchmarkSample? CurrentComparisonSample()
    {
        if (_viewModel.LastBenchmark is not { } result) return null;
        var cpu = _viewModel.HardwareItems.FirstOrDefault(x => x.Category == "CPU" && x.Name != "논리 프로세서")?.Name ?? "알 수 없는 CPU";
        var gpu = _viewModel.HardwareItems.FirstOrDefault(x => x.Category == "GPU")?.Name ?? "알 수 없는 GPU";
        var memoryGb = Math.Max(1, (int)Math.Round(_viewModel.HardwareItems.Where(x => x.Category == "메모리").Select(x => ParseLeadingNumber(x.Value)).Sum()));
        var bucket = memoryGb <= 8 ? 8 : memoryGb <= 16 ? 16 : memoryGb <= 32 ? 32 : memoryGb <= 64 ? 64 : 128;
        return new CommunityBenchmarkSample(BenchmarkVersion, BenchmarkProfile, cpu, gpu, bucket, result.OverallScore);
    }

    private static double ParseLeadingNumber(string value) => double.TryParse(new string(value.TakeWhile(c => char.IsDigit(c) || c == '.').ToArray()), out var number) ? number : 0;
    private static string FormatComparison(CommunityBenchmarkComparison value) => $"전체 {FormatPercentile(value.Overall)} · 동일 CPU {FormatPercentile(value.SameCpu)} · 동일 GPU {FormatPercentile(value.SameGpu)} · 유사 메모리 {FormatPercentile(value.SimilarMemory)}";
    private static string FormatPercentile(BenchmarkPercentile value) => value.Percentile.HasValue ? $"상위 {100 - value.Percentile.Value:0.0}% · 중앙값 {value.Median:0} · 표본 {value.SampleCount}" : $"{value.Status} · 표본 {value.SampleCount}";
}
