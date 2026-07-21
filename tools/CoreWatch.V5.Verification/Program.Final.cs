using System.IO;
using System.Reflection;
using SystemChecker.Controls;
using SystemChecker.Converters;
using SystemChecker.Services;
using CoreWatch.Installer;

namespace CoreWatch.Verification;
internal static class Program
{
    [STAThread] private static void Main()
    {
        var passed=0;var total=0;void Check(bool ok,string name){total++;if(!ok)throw new InvalidOperationException($"FAILED: {name}");passed++;Console.WriteLine($"PASS {name}");}
        Check(HardwareInventoryService.FormatBytes(32UL*1024*1024*1024).Contains("GB"),"memory uses GB");var sensor=new SensorMonitorService();var reading=sensor.Capture();Check(!string.IsNullOrWhiteSpace(reading.StatusMessage),"sensor provider status");Check(!reading.CpuTemperature.HasValue||reading.CpuTemperature is>1 and<125,"CPU temperature validity");sensor.Dispose();
        var chart=new SmoothRealtimeChart{AutoRange=true,Unit=""};var update=typeof(SmoothRealtimeChart).GetMethod("UpdateAxis",BindingFlags.Instance|BindingFlags.NonPublic)!;var minField=typeof(SmoothRealtimeChart).GetField("_axisMin",BindingFlags.Instance|BindingFlags.NonPublic)!;var maxField=typeof(SmoothRealtimeChart).GetField("_axisMax",BindingFlags.Instance|BindingFlags.NonPublic)!;update.Invoke(chart,[new List<double>{1,2,3}]);var min=Convert.ToDouble(minField.GetValue(chart));var max=Convert.ToDouble(maxField.GetValue(chart));update.Invoke(chart,[new List<double>{1.2,2.2,2.8}]);Check(Convert.ToDouble(minField.GetValue(chart))==min&&Convert.ToDouble(maxField.GetValue(chart))==max,"Y axis stable inside current range");update.Invoke(chart,[new List<double>{1,20}]);Check(Convert.ToDouble(maxField.GetValue(chart))>max,"Y axis expands on boundary breach");
        var converter=new PerformanceTierConverter();Check(converter.Convert(0d,typeof(string),"CPU",System.Globalization.CultureInfo.InvariantCulture).ToString()!.Contains("실행 후"),"benchmark pre-run label");Check(converter.Convert(1500d,typeof(string),"CPU",System.Globalization.CultureInfo.InvariantCulture).ToString()!.StartsWith("A"),"benchmark reference tier");
        var processService=new ProcessMonitorService();_=processService.CaptureAsync().GetAwaiter().GetResult();Thread.Sleep(700);var processes=processService.CaptureAsync().GetAwaiter().GetResult();Check(processes.Count>10,"process inventory");Check(processes.All(p=>p.Kind is "앱" or "백그라운드 프로세스" or "Windows 프로세스"),"process kind classification");Check(processes.Any(p=>p.Kind=="앱")&&processes.Any(p=>p.Kind=="백그라운드 프로세스"),"app and background separation");Check(processes.All(p=>p.CpuPercent is>=0 and<=100&&p.MemoryMb>=0),"process CPU and memory ranges");Check(processes.Any(p=>p.Classification=="보호된 시스템"||p.Classification=="Windows 구성요소"),"system process protection");Check(processes.FirstOrDefault(p=>p.Id==Environment.ProcessId)?.CanTerminate==false,"self termination blocked");Check(processes.Any(p=>p.Icon is not null),"process executable icons");var tracked=processes[0];var changed=false;tracked.PropertyChanged+=(_,e)=>changed|=e.PropertyName==nameof(ProcessItem.CpuPercent);tracked.UpdateUsage(new ProcessItem(tracked.Id,tracked.Name,tracked.CpuPercent==0?1:0,tracked.MemoryMb,tracked.Kind,tracked.Classification,tracked.Detail,tracked.CanTerminate,tracked.Icon));Check(changed,"process usage updates in place");var optimization=new OptimizationService();var startup=optimization.AnalyzeStartupAsync().GetAwaiter().GetResult();Check(startup is not null,"startup analysis");var cleanup=optimization.ScanCleanupAsync().GetAwaiter().GetResult();Check(cleanup.All(group=>group.SizeBytes>=0&&group.FileCount>=0),"cleanup preview safety");var optimizationSnapshot=optimization.CaptureSnapshotAsync().GetAwaiter().GetResult();Check(optimizationSnapshot.CpuPercent is>=0 and<=100&&optimizationSnapshot.MemoryPercent is>=0 and<=100,"optimization baseline metrics");Check(!PawnIoInstallDetector.RequiresInstall(new Version(2,2,0))&&PawnIoInstallDetector.RequiresInstall(new Version(2,1,0)),"installed PawnIO skip policy");var pawnVersion=PawnIoInstallDetector.GetInstalledVersion();Console.WriteLine($"PawnIO detected: {pawnVersion?.ToString()??"not installed"}");var history=new HistoryRepository();history.InitializeAsync().GetAwaiter().GetResult();Check(File.Exists(history.DatabasePath),"SQLite history database");var health=new HealthService().CaptureAsync().GetAwaiter().GetResult();Check(health.Storage is not null&&health.Battery is not null,"SMART and battery query");Console.WriteLine($"Verification passed: {passed}/{total}");
    }
}





