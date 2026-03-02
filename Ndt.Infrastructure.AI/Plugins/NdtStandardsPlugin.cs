using System.ComponentModel;
using Microsoft.SemanticKernel;
using Ndt.Domain;

namespace Ndt.Infrastructure.AI.Plugins;

public class NdtStandardsPlugin
{
    [KernelFunction]
    [Description("Checks if a specific defect exceeds the safety area threshold for a given material.")]
    public string CheckSafetyThreshold(
        [Description("The area of the defect in square pixels or mm")] double area,
        [Description("The type of material: Steel, Aluminum, or Titanium")] string material)
    {
        // Deterministic logic based on engineering standards
        double threshold = material.ToLower() switch
        {
            "steel" => 15.0,
            "aluminum" => 10.0,
            "titanium" => 5.0,
            _ => 12.0
        };

        if (area > threshold)
            return $"CRITICAL: Area {area} exceeds the threshold of {threshold} for {material}.";
        
        return $"ACCEPTABLE: Area {area} is within limits for {material}.";
    }
}