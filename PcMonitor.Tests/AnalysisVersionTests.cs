using PcMonitor.Core.Models;
namespace PcMonitor.Tests;
public sealed class AnalysisVersionTests
{
 [Fact] public void Versions_AreNotEmpty(){Assert.False(string.IsNullOrWhiteSpace(AnalysisVersionInfo.AnalysisVersion));Assert.False(string.IsNullOrWhiteSpace(AnalysisVersionInfo.RuleSetVersion));}
}
