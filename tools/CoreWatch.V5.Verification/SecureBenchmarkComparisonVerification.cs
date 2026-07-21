using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using SystemChecker.Models;
using SystemChecker.Services;

namespace CoreWatch.Verification;

internal static class SecureBenchmarkComparisonVerification
{
    [ModuleInitializer]
    internal static void Run()
    {
        static void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException($"FAILED: {name}");
            Console.WriteLine($"PASS {name}");
        }

        try { ExternalBenchmarkReferenceClient.ValidateEndpoint(new Uri("http://example.com/reference")); throw new InvalidOperationException("FAILED: HTTP endpoint blocked"); }
        catch (ArgumentException) { Console.WriteLine("PASS HTTP endpoint blocked"); }

        var comparison = new CommunityBenchmarkComparison(new(20, 75, 900, "비교 가능"), new(12, 60, 950, "비교 가능"), new(8, null, null, "표본 부족"), new(15, 70, 920, "비교 가능"));
        var response = new ExternalBenchmarkComparisonResponse(1, "corewatch-full-v1", "standard-v1", comparison);
        var handler = new RecordingHandler(JsonSerializer.Serialize(response));
        using (var client = new ExternalBenchmarkReferenceClient(handler))
        {
            var subject = new CommunityBenchmarkSample("corewatch-full-v1", "standard-v1", "CPU A", "GPU A", 32, 1000);
            var result = client.GetAsync(new Uri("https://bench.example/v1/reference"), subject).GetAwaiter().GetResult();
            Check(result.Comparison.Overall.Percentile == 75 && handler.RequestUri?.Scheme == "https" && handler.Method == HttpMethod.Get, "secure external comparison GET");
            Check(handler.RequestUri is not null && !handler.RequestUri.Query.Contains(Environment.MachineName, StringComparison.OrdinalIgnoreCase), "external query excludes machine identity");
        }

        var database = Path.Combine(Path.GetTempPath(), $"corewatch-comparison-{Guid.NewGuid():N}.db");
        try
        {
            var store = new LocalBenchmarkComparisonStore(database); var sample = new CommunityBenchmarkSample("corewatch-full-v1", "standard-v1", "CPU A", "GPU A", 32, 1000);
            store.SaveAsync(DateTimeOffset.UtcNow, sample).GetAwaiter().GetResult(); var local = store.GetCompatibleAsync(sample.BenchmarkVersion, sample.TestProfile).GetAwaiter().GetResult();
            Check(local.Count == 1 && local[0].OverallScore == 1000, "offline local benchmark comparison store");
        }
        finally { if (File.Exists(database)) File.Delete(database); }
    }

    private sealed class RecordingHandler(string json) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public HttpMethod? Method { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri; Method = request.Method;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
        }
    }
}
