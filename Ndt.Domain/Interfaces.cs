namespace Ndt.Domain;

public interface IImageProcessor
{
    byte[] ApplyHistogramStretching(byte[] inputImage, bool equalized);
    List<Defect> DetectDefects(byte[] inputImage, Rectangle roi);
    byte[] GenerateResultImage(byte[] inputImage, List<Defect> defects);
}

public interface IAiAnalysisService
{
    event Action<List<Defect>>? DefectsDetected;
    Func<string, Task<bool>>? ToolCallConfirmationAsync { get; set; }
    Task<string> AnalyzeImageAsync(byte[] image, List<Defect> defects);
    Task<string> AskQuestionAboutImageAsync(byte[] image, string userQuestion, Rectangle? roi = null);
    Task<string> AskQuestionAsync(string userQuestion);
    Task<string> AskQuestionWithManualToolCallAsync(string userQuestion, Rectangle? roi = null); // Demonstration of ToolCallBehavior.EnableKernelFunctions
    Task<string> AnalyzeWithHandlebarsAsync(byte[] image, string material, Rectangle? roi = null);
    Task<string> GetDocumentInsightAsync(string documentText, string insightType);
}

public interface IDocumentMemoryService
{
    Task ImportDocumentAsync(string documentText, string collectionName);
    Task<string> SearchRelevantContextAsync(string collectionName, string userQuery, int limit = 2);
}
