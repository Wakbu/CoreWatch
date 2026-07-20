using System.Globalization;
using System.Windows.Data;

namespace SystemChecker.Converters;

public sealed class PerformanceTierConverter:IValueConverter
{
    public object Convert(object value,Type targetType,object parameter,CultureInfo culture){var score=value is double d?d:0;if(score<=0)return"벤치마크 실행 후 기준 등급과 용도를 표시합니다.";var (tier,context)=score switch{>=1800=>("S","하이엔드"),>=1400=>("A","고성능"),>=1000=>("B","메인스트림"),>=700=>("C","일반 작업"),_=>("D","기본 작업")};var baseline=parameter?.ToString() switch{"CPU"=>"SHA-256 1,000 MiB/s = 기준 1,000점","MEMORY"=>"20 GB/s = 기준 1,000점","DISK"=>"처리량·4K IOPS 복합 기준","GPU"=>"Direct3D 125 MPix/s = 기준 1,000점",_=>"정규화 기준"};return $"{tier} · {context}  |  {baseline}";}public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture)=>Binding.DoNothing;
}

