using PcMonitor.Core.Models;
namespace PcMonitor.Application.Analysis;
public interface IAnalysisQualityService
{
 AnalysisQualityReport Evaluate(RunSummary run, RunSampleCounts? counts,
  IReadOnlyList<FrameChartPoint> framePoints, IReadOnlyList<FrameRunAnalysis> frameAnalyses);
}
