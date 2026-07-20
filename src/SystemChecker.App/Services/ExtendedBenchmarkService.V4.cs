using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using SystemChecker.Models;

namespace SystemChecker.Services;

public sealed class ExtendedBenchmarkService
{
    private readonly BenchmarkService _cpuMemory = new();
    public async Task<FullBenchmarkResult> RunAsync(IProgress<string>? progress, CancellationToken token)
    {
        var baseResult=await _cpuMemory.RunAsync(progress,token);
        var disk=await RunDiskAsync(progress,token);
        var gpu=await Application.Current.Dispatcher.InvokeAsync(()=>RunGpu(progress,token));
        var overall=(int)Math.Round(baseResult.CpuScore*.35+baseResult.MemoryScore*.20+disk.Score*.25+gpu.Score*.20);
        var grade=overall switch{>=1500=>"S",>=1100=>"A",>=800=>"B",>=500=>"C",_=>"D"};
        return new(DateTimeOffset.Now,baseResult,disk,gpu,overall,grade,"첫 측정");
    }
    private static async Task<DiskBenchmarkResult> RunDiskAsync(IProgress<string>? progress,CancellationToken token)
    {
        var exe=Path.Combine(AppContext.BaseDirectory,"tools","diskspd.exe");
        if(!File.Exists(exe)) throw new FileNotFoundException("번들 DiskSpd를 찾지 못했습니다.",exe);
        var target=Path.Combine(Path.GetTempPath(),$"systemchecker-{Environment.ProcessId}.dat");
        try
        {
            progress?.Report("DiskSpd · 순차 읽기");var read=await RunDiskSpd(exe,target,"-b1M -d3 -W1 -o4 -t1 -Sh -L -Rxml -c256M",token);
            progress?.Report("DiskSpd · 순차 쓰기");var write=await RunDiskSpd(exe,target,"-b1M -d3 -W1 -o4 -t1 -w100 -Sh -L -Rxml",token);
            progress?.Report("DiskSpd · 4K 랜덤 혼합");var random=await RunDiskSpd(exe,target,"-b4K -d3 -W1 -o32 -t1 -r -w30 -Sh -L -Rxml",token);
            var score=(int)Math.Round(Math.Min(2000,read.ReadMbps/3+write.WriteMbps/3+(random.ReadIops+random.WriteIops)/250));
            return new(read.ReadMbps,write.WriteMbps,random.ReadIops,random.WriteIops,random.Latency,score,"Microsoft DiskSpd 2.2");
        }
        finally{try{if(File.Exists(target))File.Delete(target);}catch{}}
    }
    private static async Task<(double ReadMbps,double WriteMbps,double ReadIops,double WriteIops,double Latency)> RunDiskSpd(string exe,string target,string args,CancellationToken token)
    {
        var info=new ProcessStartInfo(exe){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};info.Arguments=$"{args} \"{target}\"";
        using var p=Process.Start(info)??throw new InvalidOperationException("DiskSpd 실행 실패");var output=p.StandardOutput.ReadToEndAsync(token);var error=p.StandardError.ReadToEndAsync(token);await p.WaitForExitAsync(token);if(p.ExitCode!=0)throw new InvalidOperationException((await error).Trim());
        var doc=XDocument.Parse(await output);var seconds=Num(doc.Descendants("TestTimeSeconds").FirstOrDefault()?.Value);var targets=doc.Descendants("Thread").Elements("Target").ToList();var rb=targets.Sum(x=>Num(x.Element("ReadBytes")?.Value));var wb=targets.Sum(x=>Num(x.Element("WriteBytes")?.Value));var rc=targets.Sum(x=>Num(x.Element("ReadCount")?.Value));var wc=targets.Sum(x=>Num(x.Element("WriteCount")?.Value));var latency=Num(doc.Descendants("AverageTotalMilliseconds").FirstOrDefault()?.Value);return(rb/seconds/1048576,wb/seconds/1048576,rc/seconds,wc/seconds,latency);
    }
    private static double Num(string? value)=>double.TryParse(value,NumberStyles.Any,CultureInfo.InvariantCulture,out var d)&&d>0?d:0.0001;
    private static GpuBenchmarkResult RunGpu(IProgress<string>? progress,CancellationToken token)
    {
        progress?.Report("GPU · WPF Direct3D 렌더링");const int width=1280,height=720;var visual=new DrawingVisual{Effect=new BlurEffect{Radius=8,KernelType=KernelType.Gaussian}};using(var dc=visual.RenderOpen()){for(var i=0;i<100;i++){var hue=(byte)(40+i*2);dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(hue,(byte)(220-i),200)),null,new Point((i*83)%width,(i*47)%height),90,90);}}var target=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);for(var i=0;i<5;i++)target.Render(visual);var watch=Stopwatch.StartNew();var frames=0;while(watch.Elapsed<TimeSpan.FromSeconds(3)){token.ThrowIfCancellationRequested();target.Render(visual);frames++;}var fps=frames/watch.Elapsed.TotalSeconds;var mpix=fps*width*height/1_000_000d;var tier=RenderCapability.Tier>>16;var hw=tier>=2;var score=(int)Math.Round(Math.Min(2000,mpix*8));return new(fps,mpix,tier,hw,score,hw?"Direct3D 하드웨어 렌더링":"WPF 소프트웨어 렌더링 대체 경로");
    }
}
