using System.Collections;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace SystemChecker.Controls;

public sealed class SmoothRealtimeChart : FrameworkElement
{
    public static readonly DependencyProperty ValuesProperty=DependencyProperty.Register(nameof(Values),typeof(IEnumerable),typeof(SmoothRealtimeChart),new FrameworkPropertyMetadata(null,OnValuesChanged));
    public static readonly DependencyProperty StrokeProperty=DependencyProperty.Register(nameof(Stroke),typeof(Brush),typeof(SmoothRealtimeChart),new FrameworkPropertyMetadata(Brushes.DodgerBlue,FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty AutoRangeProperty=DependencyProperty.Register(nameof(AutoRange),typeof(bool),typeof(SmoothRealtimeChart),new FrameworkPropertyMetadata(false,FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty MinimumProperty=DependencyProperty.Register(nameof(Minimum),typeof(double),typeof(SmoothRealtimeChart),new FrameworkPropertyMetadata(0d,FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty MaximumProperty=DependencyProperty.Register(nameof(Maximum),typeof(double),typeof(SmoothRealtimeChart),new FrameworkPropertyMetadata(100d,FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty UnitProperty=DependencyProperty.Register(nameof(Unit),typeof(string),typeof(SmoothRealtimeChart),new FrameworkPropertyMetadata(string.Empty,FrameworkPropertyMetadataOptions.AffectsRender));
    public IEnumerable? Values{get=>(IEnumerable?)GetValue(ValuesProperty);set=>SetValue(ValuesProperty,value);}public Brush Stroke{get=>(Brush)GetValue(StrokeProperty);set=>SetValue(StrokeProperty,value);}public bool AutoRange{get=>(bool)GetValue(AutoRangeProperty);set=>SetValue(AutoRangeProperty,value);}public double Minimum{get=>(double)GetValue(MinimumProperty);set=>SetValue(MinimumProperty,value);}public double Maximum{get=>(double)GetValue(MaximumProperty);set=>SetValue(MaximumProperty,value);}public string Unit{get=>(string)GetValue(UnitProperty);set=>SetValue(UnitProperty,value);}
    private List<double> _values=[];private readonly Stopwatch _motion=new();private bool _rendering,_queued;private double _axisMin=double.NaN,_axisMax=double.NaN;private DateTimeOffset _lastAxisExpansion=DateTimeOffset.MinValue;
    public SmoothRealtimeChart(){SnapsToDevicePixels=true;Unloaded+=(_,_)=>Stop();}
    private static void OnValuesChanged(DependencyObject d,DependencyPropertyChangedEventArgs e){var c=(SmoothRealtimeChart)d;if(e.OldValue is INotifyCollectionChanged old)old.CollectionChanged-=c.Changed;if(e.NewValue is INotifyCollectionChanged current)current.CollectionChanged+=c.Changed;c.Queue();}
    private void Changed(object? sender,NotifyCollectionChangedEventArgs e)=>Queue();
    private void Queue(){if(_queued)return;_queued=true;Dispatcher.BeginInvoke(DispatcherPriority.DataBind,()=>{_queued=false;var next=Values?.Cast<object>().Select(Convert.ToDouble).ToList()??[];if(next.Count==0)return;_values=next;UpdateAxis(next);_motion.Restart();if(!_rendering){CompositionTarget.Rendering+=RenderFrame;_rendering=true;}InvalidateVisual();});}
    private void RenderFrame(object? sender,EventArgs e){InvalidateVisual();if(_motion.ElapsedMilliseconds>=500)Stop();}
    private void Stop(){if(!_rendering)return;CompositionTarget.Rendering-=RenderFrame;_rendering=false;}
    private void UpdateAxis(IReadOnlyCollection<double> values)
    {
        if(!AutoRange){_axisMin=Minimum;_axisMax=Maximum;return;}var rawMin=values.Min();var rawMax=values.Max();double wantedMin,wantedMax;
        if(Unit=="%") {wantedMin=Math.Max(0,Math.Floor(rawMin/5)*5);wantedMax=Math.Min(100,Math.Ceiling(rawMax/5)*5);if(wantedMax-wantedMin<10){wantedMin=Math.Max(0,wantedMin-5);wantedMax=Math.Min(100,wantedMin+10);}}
        else{wantedMin=0;wantedMax=NiceCeiling(Math.Max(.01,rawMax*1.15));}
        var now=DateTimeOffset.Now;if(double.IsNaN(_axisMax)||rawMin<_axisMin||rawMax>_axisMax){_axisMin=wantedMin;_axisMax=Math.Max(wantedMin+.01,wantedMax);_lastAxisExpansion=now;}
        else if(now-_lastAxisExpansion>TimeSpan.FromSeconds(30)&&(wantedMax<_axisMax*.65||wantedMin>_axisMin+(Unit=="%"?5:0))){_axisMin=wantedMin;_axisMax=Math.Max(wantedMin+.01,wantedMax);_lastAxisExpansion=now;}
    }
    private static double NiceCeiling(double value){var exponent=Math.Pow(10,Math.Floor(Math.Log10(value)));var normalized=value/exponent;var nice=normalized<=1?1:normalized<=2?2:normalized<=5?5:10;return nice*exponent;}
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);if(ActualWidth<80||ActualHeight<50)return;var dpi=VisualTreeHelper.GetDpi(this).PixelsPerDip;var left=42d;var top=7d;var right=ActualWidth-8;var bottom=ActualHeight-20;var width=Math.Max(1,right-left);var height=Math.Max(1,bottom-top);var min=double.IsNaN(_axisMin)?Minimum:_axisMin;var max=double.IsNaN(_axisMax)?Maximum:_axisMax;var grid=new Pen(new SolidColorBrush(Color.FromRgb(224,228,234)),1);
        for(var i=0;i<=4;i++){var y=top+height*i/4d;dc.DrawLine(grid,new Point(left,y),new Point(right,y));var value=max-(max-min)*i/4d;var label=new FormattedText(Format(value),CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),9,new SolidColorBrush(Color.FromRgb(119,128,145)),dpi);dc.DrawText(label,new Point(Math.Max(0,left-label.Width-7),y-label.Height/2));}
        DrawLabel(dc,"60초",left,bottom+5,dpi);var nowText=Text("현재",dpi);dc.DrawText(nowText,new Point(right-nowText.Width,bottom+5));if(_values.Count<2)return;
        const int capacity=120;var dx=width/(capacity-1d);var progress=Math.Clamp(_motion.Elapsed.TotalMilliseconds/500d,0,1);var smooth=progress*progress*(3-2*progress);var offset=dx*(1-smooth);var start=right-dx*(_values.Count-1);var geometry=new StreamGeometry();using(var ctx=geometry.Open()){for(var i=0;i<_values.Count;i++){var point=new Point(start+dx*i+offset,bottom-Math.Clamp((_values[i]-min)/Math.Max(.0001,max-min),0,1)*height);if(i==0)ctx.BeginFigure(point,false,false);else ctx.LineTo(point,true,false);}}geometry.Freeze();var pen=new Pen(Stroke,1.8){LineJoin=PenLineJoin.Round,StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round};dc.PushClip(new RectangleGeometry(new Rect(left,top,width,height)));dc.DrawGeometry(null,pen,geometry);dc.Pop();
    }
    private void DrawLabel(DrawingContext dc,string value,double x,double y,double dpi)=>dc.DrawText(Text(value,dpi),new Point(x,y));private static FormattedText Text(string value,double dpi)=>new(value,CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),9,new SolidColorBrush(Color.FromRgb(145,153,167)),dpi);
    private string Format(double value)=>(Math.Abs(value)>=100?value.ToString("0"):Math.Abs(value)>=10?value.ToString("0.0"):value.ToString("0.00"))+Unit;
}

