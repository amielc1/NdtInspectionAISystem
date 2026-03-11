using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace Ndt.Infrastructure.AI.Plugins;

public class NdtReportingPlugin
{
    [KernelFunction]
    [Description("Submits a final inspection report to the central database.")]
    public string SubmitDefectReport(
        [Description("The full summary of the inspection results")] string report,
        [Description("The final status: PASS or FAIL")] string status)
    {
        // In a real app, this would be a database call.
        // For demonstration, we just return a success message.
        return $"SUCCESS: Report with status '{status}' has been submitted to the database. content: {report.Substring(0, Math.Min(report.Length, 50))}...";
    }
}
