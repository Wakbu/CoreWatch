using Microsoft.Data.Sqlite;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SystemChecker.Models;
using SystemChecker.Services;

namespace SystemChecker;

public partial class TelemetryMainWindow
{
    private const string LocalBenchmarkVersion = "corewatch-full-v1";
    private const string LocalBenchmarkProfile = "standard-v1";
    private readonly LocalBenchmarkComparisonStore _localComparisonStore = new();
    private TextBlock? _localComparisonStatus;

    private void InitializeLocalBenchmarkComparison()
    {
        if (BenchmarkPage.Content is not StackPanel root) return; var panel = Panel(); panel.Margin = new Thickness(0, 12, 0, 0); var stack = new StackPanel(); panel.Child = stack;
        stack.Children.Add(Heading("LOCAL BENCHMARK COMPARISON")); stack.Children.Add(new TextBlock { Text = "인터넷 연결과 외부 전송 없이 동일 벤치마크 버전·프로필의 이 PC 기록만 비교합니다.", Foreground = new SolidColorBrush(Color.FromRgb(104, 112, 125)), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 7, 0, 12) });
        _localComparisonStatus = new TextBlock { Text = "벤치마크 실행 후 로컬 비교 기록을 표시합니다.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) }; stack.Children.Add(_localComparisonStatus); stack.Children.Add(Action("내 기록으로 비교", async (_, _) => await RefreshLocalComparisonAsync(false, _monitorCancellation.Token))); root.Children.Add(panel);
    }

    private async Task RefreshLocalComparisonAsync(bool saveCurrent, CancellationToken token)
    {
        if (_viewModel.LastBenchmark is not { } result) { if (_localComparisonStatus is not null) _localComparisonStatus.Text = "먼저 벤치마크를 실행하세요."; return; }
        try
        {
            var sample = new LocalBenchmarkSample(LocalBenchmarkVersion, LocalBenchmarkProfile, result.OverallScore); if (saveCurrent) await _localComparisonStore.SaveAsync(result.MeasuredAt, sample, token); var scores = await _localComparisonStore.GetScoresAsync(sample.BenchmarkVersion, sample.TestProfile, token); var comparison = LocalBenchmarkComparisonService.Compare(sample.OverallScore, scores);
            if (_localComparisonStatus is not null) _localComparisonStatus.Text = comparison.SampleCount == 0 ? "호환되는 로컬 기록이 없습니다." : $"내 기록 {comparison.SampleCount}개 · 상위 {100 - comparison.Percentile:0.0}% · 중앙값 {comparison.Median:0}점";
        }
        catch (Exception ex) when (ex is SqliteException or IOException) { if (_localComparisonStatus is not null) _localComparisonStatus.Text = $"로컬 비교 저장소 오류: {ex.Message}"; }
    }
}
