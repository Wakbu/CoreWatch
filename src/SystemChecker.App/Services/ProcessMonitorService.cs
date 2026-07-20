using System.Diagnostics;

namespace SystemChecker.Services;

public sealed record ProcessItem(int Id,string Name,double CpuPercent,double MemoryMb,string Classification,string Detail,bool CanTerminate);
public sealed class ProcessMonitorService
{
    private readonly Dictionary<int,(TimeSpan Cpu,DateTimeOffset At)> _previous=[];
    private static readonly HashSet<string> Critical=new(StringComparer.OrdinalIgnoreCase){"system","registry","smss","csrss","wininit","winlogon","services","lsass","svchost","fontdrvhost","sihost","dwm"};
    public Task<IReadOnlyList<ProcessItem>> CaptureAsync(CancellationToken token=default)=>Task.Run(()=>Capture(token),token);
    private IReadOnlyList<ProcessItem> Capture(CancellationToken token)
    {
        var now=DateTimeOffset.Now;var current=new Dictionary<int,(TimeSpan,DateTimeOffset)>();var list=new List<ProcessItem>();foreach(var p in Process.GetProcesses()){token.ThrowIfCancellationRequested();try{var cpu=p.TotalProcessorTime;current[p.Id]=(cpu,now);var percent=_previous.TryGetValue(p.Id,out var old)?Math.Clamp((cpu-old.Cpu).TotalMilliseconds/Math.Max(1,(now-old.At).TotalMilliseconds)/Environment.ProcessorCount*100,0,100):0;var path=TryPath(p);var classification=Classify(p.Id,p.ProcessName,path);var canTerminate=classification=="일반 프로그램"&&p.Id!=Environment.ProcessId;list.Add(new(p.Id,p.ProcessName,percent,p.WorkingSet64/1048576d,classification,string.IsNullOrWhiteSpace(path)?"실행 경로 확인 불가":path,canTerminate));}catch{}finally{p.Dispose();}}_previous.Clear();foreach(var pair in current)_previous[pair.Key]=pair.Value;return list.OrderByDescending(x=>x.CpuPercent).ThenByDescending(x=>x.MemoryMb).ToList();
    }
    public async Task TerminateAsync(ProcessItem item,CancellationToken token=default){if(!item.CanTerminate)throw new InvalidOperationException("CoreWatch는 보호된 시스템 구성요소나 경로를 확인할 수 없는 프로세스를 종료하지 않습니다.");using var p=Process.GetProcessById(item.Id);if(p.HasExited)return;if(p.CloseMainWindow()){try{await p.WaitForExitAsync(token).WaitAsync(TimeSpan.FromSeconds(2),token);return;}catch(TimeoutException){}}p.Kill(true);await p.WaitForExitAsync(token);}
    private static string TryPath(Process p){try{return p.MainModule?.FileName??string.Empty;}catch{return string.Empty;}}
    private static string Classify(int id,string name,string path){if(id<=4||Critical.Contains(name))return"보호된 시스템";var windows=Environment.GetFolderPath(Environment.SpecialFolder.Windows).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;if(!string.IsNullOrWhiteSpace(path)&&path.StartsWith(windows,StringComparison.OrdinalIgnoreCase))return"Windows 구성요소";if(string.IsNullOrWhiteSpace(path))return"확인 필요";return"일반 프로그램";}
}
