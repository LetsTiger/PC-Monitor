using PcMonitor.Core.Models;
namespace PcMonitor.Application.Analysis;
public sealed class AnalysisQualityService : IAnalysisQualityService
{
 public AnalysisQualityReport Evaluate(RunSummary run, RunSampleCounts? counts,
  IReadOnlyList<FrameChartPoint> points, IReadOnlyList<FrameRunAnalysis> frames)
 {
  var issues=new List<string>(); double score=100;
  double duration=Math.Max(0,run.Duration.TotalSeconds);
  if(duration<30){issues.Add("Der Run ist kürzer als 30 Sekunden.");score-=35;}
  else if(duration<120){issues.Add("Für belastbare Trends werden mindestens zwei Minuten empfohlen.");score-=10;}
  long frameCount=frames.FirstOrDefault(x=>x.IsLikelyGame)?.FrameCount ?? frames.FirstOrDefault()?.FrameCount ?? 0;
  if(frameCount<120){issues.Add("Zu wenige FPS-Samples.");score-=40;}
  long sys=counts?.SystemSamples??0, hw=counts?.HardwareSamples??0, proc=counts?.ProcessSamples??0;
  if(sys==0){issues.Add("Keine Systemmesswerte.");score-=20;}
  if(hw==0){issues.Add("Keine Hardwaremesswerte.");score-=20;}
  if(proc==0){issues.Add("Keine Prozessmesswerte.");score-=20;}
  double? coverage=points.Count>1?(points[^1].Timestamp-points[0].Timestamp).TotalSeconds:null;
  double? gap=null;
  if(points.Count>1) gap=points.Zip(points.Skip(1),(a,b)=>(b.Timestamp-a.Timestamp).TotalSeconds).Max();
  if(gap>5){issues.Add($"FPS-Messlücke von {gap:F1} Sekunden erkannt.");score-=20;}
  if(coverage is not null && duration>0 && coverage/duration<0.65){issues.Add("FPS-Daten decken weniger als 65 % des Runs ab.");score-=20;}
  score=Math.Clamp(score,0,100);
  var level=score>=90?AnalysisQualityLevel.Excellent:score>=75?AnalysisQualityLevel.Good:score>=45?AnalysisQualityLevel.Limited:AnalysisQualityLevel.Insufficient;
  string summary=level switch{AnalysisQualityLevel.Excellent=>"Sehr gute Datengrundlage.",AnalysisQualityLevel.Good=>"Gute Datengrundlage.",AnalysisQualityLevel.Limited=>"Analyse nur eingeschränkt belastbar.",_=>"Keine belastbare Analyse möglich."};
  return new(run.Id,level,score,summary,issues,sys,hw,proc,frameCount,coverage,gap);
 }
}
