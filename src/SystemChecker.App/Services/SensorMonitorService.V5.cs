using LibreHardwareMonitor.Hardware;
using System.Management;

namespace SystemChecker.Services;

public sealed record SensorSummary(double? CpuTemperature,double? GpuTemperature,double? CpuClock,double? CpuPower,double? MaximumFanRpm,bool IsAvailable,string StatusMessage);
public sealed class SensorMonitorService:IDisposable
{
    private readonly Computer _computer=new(){IsCpuEnabled=true,IsGpuEnabled=true,IsMotherboardEnabled=true,IsMemoryEnabled=true,IsStorageEnabled=true,IsControllerEnabled=true};private readonly Visitor _visitor=new();private bool _available;private string _status="센서 초기화 중";private double? _fallbackTemperature;private DateTimeOffset _lastFallback=DateTimeOffset.MinValue;
    public SensorMonitorService(){try{_computer.Open();_computer.Accept(_visitor);_available=true;_status="센서 연결됨";}catch(Exception ex){_status=$"센서 접근 실패: {ex.Message}";}}
    public SensorSummary Capture()
    {
        if(!_available)return new(null,null,null,null,null,false,_status);try{_computer.Accept(_visitor);var sensors=Flatten(_computer.Hardware).SelectMany(h=>h.Sensors).Where(s=>s.Value.HasValue).ToArray();var cpu=sensors.Where(s=>IsCpu(s.Hardware)).ToArray();var gpu=sensors.Where(s=>IsGpu(s.Hardware)).ToArray();var (temperature,source)=CpuTemperature(cpu,sensors);if(!temperature.HasValue){if(DateTimeOffset.Now-_lastFallback>TimeSpan.FromSeconds(3)){_lastFallback=DateTimeOffset.Now;_fallbackTemperature=ReadWindowsThermalZone();}temperature=_fallbackTemperature;if(temperature.HasValue)source="Windows ACPI";}
            var pawn=LibreHardwareMonitor.PawnIo.PawnIo.IsInstalled;_status=temperature.HasValue?$"CPU 온도 정상 · {source}":pawn?"PawnIO 연결됨 · 이 보드의 CPU 온도 센서를 찾지 못함":"CPU 온도용 PawnIO 드라이버가 설치되지 않음";return new(temperature,Max(gpu,SensorType.Temperature,1,125),Average(cpu,SensorType.Clock,100,10000),Max(cpu,SensorType.Power,.01,2000),Max(sensors,SensorType.Fan,1,20000),true,_status);
        }catch(Exception ex){_status=$"센서 갱신 실패: {ex.Message}";return new(null,null,null,null,null,false,_status);}
    }
    private static (double? Value,string Source) CpuTemperature(IEnumerable<ISensor> cpu,IEnumerable<ISensor> all)
    {
        var valid=cpu.Where(ValidTemperature).ToArray();var source="CPU package";if(valid.Length==0){string[] boardNames=["CPU","Tctl","Tdie","Package","Socket","PECI"];valid=all.Where(ValidTemperature).Where(s=>boardNames.Any(n=>s.Name.Contains(n,StringComparison.OrdinalIgnoreCase))).ToArray();source="메인보드 센서";}if(valid.Length==0)return(null,string.Empty);string[] preferred=["Tctl/Tdie","Package","Tctl","Tdie","Core Max","CPU Core","CPU"];foreach(var name in preferred){var match=valid.FirstOrDefault(s=>s.Name.Contains(name,StringComparison.OrdinalIgnoreCase));if(match?.Value is float value)return(value,$"{source} · {match.Name}");}return(valid.Max(s=>(double)s.Value!.Value),source);
    }
    private static bool ValidTemperature(ISensor s)=>s.SensorType==SensorType.Temperature&&s.Value is>1 and<125;
    private static double? ReadWindowsThermalZone(){foreach(var query in new[]{(@"root\WMI","SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature","CurrentTemperature"),(@"root\CIMV2","SELECT Temperature FROM Win32_PerfFormattedData_Counters_ThermalZoneInformation","Temperature")})try{using var searcher=new ManagementObjectSearcher(query.Item1,query.Item2);var values=searcher.Get().Cast<ManagementObject>().Select(o=>Convert.ToDouble(o[query.Item3])).Select(v=>query.Item3=="CurrentTemperature"?v/10d-273.15:v-273.15).Where(v=>v>1&&v<125).ToArray();if(values.Length>0)return values.Max();}catch{}return null;}
    private static IEnumerable<IHardware> Flatten(IEnumerable<IHardware> hardware){foreach(var item in hardware){yield return item;foreach(var child in Flatten(item.SubHardware))yield return child;}}private static bool IsCpu(IHardware h)=>h.HardwareType.ToString().Equals("Cpu",StringComparison.OrdinalIgnoreCase);private static bool IsGpu(IHardware h)=>h.HardwareType.ToString().StartsWith("Gpu",StringComparison.OrdinalIgnoreCase);
    private static double? Max(IEnumerable<ISensor>s,SensorType t,double min,double max){var v=s.Where(x=>x.SensorType==t&&x.Value.HasValue).Select(x=>(double)x.Value!.Value).Where(x=>x>=min&&x<=max).ToArray();return v.Length==0?null:v.Max();}private static double? Average(IEnumerable<ISensor>s,SensorType t,double min,double max){var v=s.Where(x=>x.SensorType==t&&x.Value.HasValue).Select(x=>(double)x.Value!.Value).Where(x=>x>=min&&x<=max).ToArray();return v.Length==0?null:v.Average();}
    public void Dispose(){if(!_available)return;_computer.Close();_available=false;}private sealed class Visitor:IVisitor{public void VisitComputer(IComputer c)=>c.Traverse(this);public void VisitHardware(IHardware h){h.Update();foreach(var child in h.SubHardware)child.Accept(this);}public void VisitSensor(ISensor s){}public void VisitParameter(IParameter p){}}
}

