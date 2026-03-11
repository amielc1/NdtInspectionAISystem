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

    public int RoiX { get; set; } = 0;
    public int RoiY { get; set; } = 0;
    public int RoiWidth { get; set; } = 0;
    public int RoiHeight { get; set; } = 0;

    [KernelFunction]
    [Description("Runs an OpenCV analysis on the current weld image to detect defects like porosity or cracks.")]
    public List<Defect> DetectDefects()
    {
        if (CurrentImage == null) return new List<Defect>();
        
        // We use the ROI set by the user or default (0,0,0,0) for the AI-triggered scan
        var roi = new Rectangle(RoiX, RoiY, RoiWidth, RoiHeight); 
        var defects = imageProcessor.DetectDefects(CurrentImage, roi);
        
        DefectsDetected?.Invoke(defects);
        
        return defects;
    }
}