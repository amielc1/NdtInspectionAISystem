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
    Task<string> AnalyzeImageAsync(byte[] image, List<Defect> defects);
    Task<string> AskQuestionAboutImageAsync(byte[] image, string userQuestion);
    Task<string> AskQuestionAsync(string userQuestion);
    Task<string> AnalyzeWithHandlebarsAsync(byte[] image, string material);
}
