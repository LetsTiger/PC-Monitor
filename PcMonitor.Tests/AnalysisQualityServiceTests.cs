using PcMonitor.Application.Analysis;
using PcMonitor.Core.Models;
namespace PcMonitor.Tests;
public sealed class AnalysisQualityServiceTests
{
 [Fact] public void ShortRunWithoutFrames_IsInsufficient()
 {
  var run=new RunSummary(Guid.NewGuid(),"test",DateTimeOffset.UtcNow,DateTimeOffset.UtcNow.AddSeconds(10),RunStatus.Completed,0);
  var sut=new AnalysisQualityService();
  var result=sut.Evaluate(run,null,[],[]);
  Assert.Equal(AnalysisQualityLevel.Insufficient,result.Level);
  Assert.Contains(result.Issues,x=>x.Contains("kürzer"));
 }
 [Fact] public void LongRunWithCoverage_IsGoodOrBetter()
 {
  var start=DateTimeOffset.UtcNow;
  var run=new RunSummary(Guid.NewGuid(),"test",start,start.AddMinutes(3),RunStatus.Completed,1000);
  var counts=new RunSampleCounts(run.Id,180,180,180,0,0,180,0);
  var points=Enumerable.Range(0,180).Select(i=>new FrameChartPoint(start.AddSeconds(i),i,60,16.6,false,30,40,95,60,50,2,0,true,true,null,null)).ToArray();
  var frames=new[]{new FrameRunAnalysis(run.Id,"game.exe",1,10000,start,start.AddMinutes(3),60,50,80,55,45,16.6,20,24,30,50,1,0,0,180,170,94,30,50,180,90,true,1,"game",FrameProcessMatchReason.ExactProcessId)};
  var result=new AnalysisQualityService().Evaluate(run,counts,points,frames);
  Assert.True(result.Level>=AnalysisQualityLevel.Good);
 }
}
