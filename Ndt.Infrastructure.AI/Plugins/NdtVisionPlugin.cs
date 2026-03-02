// Ndt.Infrastructure.AI/Plugins/NdtVisionPlugin.cs
using System.ComponentModel;
using Microsoft.SemanticKernel;
using Ndt.Domain;

namespace Ndt.Infrastructure.AI.Plugins;

public class NdtVisionPlugin(IImageProcessor imageProcessor)
{
    public event Action<List<Defect>>? DefectsDetected;

    // We store the current image in the plugin's state or pass it via context
    public byte[]? CurrentImage { get; set; }

    [KernelFunction]
    [Description("Runs an OpenCV analysis on the current weld image to detect defects like porosity or cracks.")]
    public List<Defect> DetectDefects()
    {
        if (CurrentImage == null) return new List<Defect>();
        
        // We use a default ROI (the whole image) for the AI-triggered scan
        var fullRoi = new Rectangle(0, 0, 0, 0); // Logic inside processor handles '0' as full
        var defects = imageProcessor.DetectDefects(CurrentImage, fullRoi);
        
        DefectsDetected?.Invoke(defects);
        
        return defects;
    }
}