using System.IO;
using System.Reflection;
using SystemChecker.Controls;
using SystemChecker.Converters;
using SystemChecker.Services;

namespace CoreWatch.Verification;
internal static class Program
{
    [STAThread] private static void Main()
    {
        var passed=0;var total=0;void Check(bool ok,string name){total++;if(!ok)throw new InvalidOperationException($"FAILED: {name}");passed++;Console.WriteLine($"PASS {name}");}
        Check(HardwareInventoryService.FormatBytes(32UL*1024*1024*1024).Contains("GB"),"memory uses GB");var sensor=new SensorMonitorService();var reading=sensor.Capture();Check(!string.IsNullOrWhiteSpace(reading.StatusMessage),"sensor provider status");Check(!reading.CpuTemperature.HasValue||reading.CpuTemperature is>1 and<125,"CPU temperature validity");sensor.Dispose();
        var chart=new SmoothRealtimeChart{AutoRange=true,Unit=""};var update=typeof(SmoothRealtimeChart).GetMethod("UpdateAxis",BindingFlags.Instance|BindingFlags.NonPublic)!;var minField=typeof(SmoothRealtimeChart).GetField("_axisMin",BindingFlags.Instance|BindingFlags.NonPublic)!;var maxField=typeof(SmoothRealtimeChart).GetField("_axisMax",BindingFlags.Instance|BindingFlags.NonPublic)!;update.Invoke(chart,[new List<double>{1,2,3}]);var min=Convert.ToDouble(minField.GetValue(chart));var max=Convert.ToDouble(maxField.GetValue(chart));update.Invoke(chart,[new List<double>{1.2,2.2,2.8}]);Check(Convert.ToDouble(minField.GetValue(chart))==min&&Convert.ToDouble(maxField.GetValue(chart))==max,"Y axis stable inside current range");update.Invoke(chart,[new List<double>{1,20}]);Check(Convert.ToDouble(maxField.GetValue(chart))>max,"Y axis expands on boundary breach");
        var converter=new PerformanceTierConverter();Check(converter.Convert(0d,typeof(string),"CPU",System.Globalization.CultureInfo.InvariantCulture).ToString()!.Contains("실행 후"),"benchmark pre-run label");Check(converter.Convert(1500d,typeof(string),"CPU",System.Globalization.CultureInfo.InvariantCulture).ToString()!.StartsWith("A"),"benchmark reference tier");
        var processService=new ProcessMonitorService();_=processService.CaptureAsync().GetAwaiter().GetResult();Thread.Sleep(700);var processes=processService.CaptureAsync().GetAwaiter().GetResult();Check(processes.Count>10,"process inventory");Check(processes.All(p=>p.CpuPercent is>=0 and<=100&&p.MemoryMb>=0),"process CPU and memory ranges");Check(processes.Any(p=>p.Classification=="보호된 시스템"||p.Classification=="Windows 구성요소"),"system process protection");Check(processes.FirstOrDefault(p=>p.Id==Environment.ProcessId)?.CanTerminate==false,"self termination blocked");var history=new HistoryRepository();history.InitializeAsync().GetAwaiter().GetResult();Check(File.Exists(history.DatabasePath),"SQLite history database");var health=new HealthService().CaptureAsync().GetAwaiter().GetResult();Check(health.Storage is not null&&health.Battery is not null,"SMART and battery query");Console.WriteLine($"Verification passed: {passed}/{total}");
    }
}

