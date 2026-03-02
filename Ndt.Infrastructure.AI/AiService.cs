using System.Reflection;
using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Google;
using Ndt.Domain;
using Ndt.Infrastructure.AI.Plugins; 
using Microsoft.SemanticKernel.ChatCompletion;

namespace Ndt.Infrastructure.AI;

public class AiService  : IAiAnalysisService
{
    public event Action<List<Defect>>? DefectsDetected;
    
    private readonly Kernel _kernel;
    private readonly NdtVisionPlugin _visionPlugin;

    public AiService(Kernel kernel, IImageProcessor imageProcessor)
    {
        _kernel = kernel;
        // Manual instantiation of the plugin to keep a reference to the image state
        _visionPlugin = new NdtVisionPlugin(imageProcessor);
        _visionPlugin.DefectsDetected += defects => DefectsDetected?.Invoke(defects);
        
        _kernel.Plugins.AddFromObject(_visionPlugin, "WeldVision");
        _kernel.Plugins.AddFromType<NdtStandardsPlugin>("StandardsProvider");
    }
    public async Task<string> AskQuestionAboutImageAsync(byte[] image, string userQuestion)
    {
        // 1. Update the plugin with the latest image from the UI
        _visionPlugin.CurrentImage = image;

        var chat = _kernel.GetRequiredService<IChatCompletionService>();
        
        // 2. Setup the "Thinking" environment 
        var settings = new GeminiPromptExecutionSettings 
        { 
            ToolCallBehavior = GeminiToolCallBehavior.AutoInvokeKernelFunctions
        };

        var history = new ChatHistory("You are a helpful NDT assistant. Use your tools to analyze images and standards.");
        history.AddUserMessage(userQuestion);

        // 3. The AI will call DetectDefects() and CheckSafetyThreshold() automatically if needed
        var result = await chat.GetChatMessageContentAsync(history, settings, _kernel);
        
        return result.ToString();
    }
    public async Task<string> AnalyzeImageAsync(byte[] image, List<Defect> defects)
    {
        // 1. Load the prompt from Embedded Resources
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream("Ndt.Infrastructure.AI.Prompts.WeldAnalysis.skprompt.txt");
        using var reader = new StreamReader(stream!);
        var promptTemplate = await reader.ReadToEndAsync();

        // 2. Create the function from the loaded template
        var analysisFunction = _kernel.CreateFunctionFromPrompt(promptTemplate);

        // 3. Setup plugins and execution settings (as before)
        _kernel.ImportPluginFromType<NdtStandardsPlugin>("StandardsProvider");
        
        var settings = new GeminiPromptExecutionSettings 
        { 
            ToolCallBehavior = GeminiToolCallBehavior.AutoInvokeKernelFunctions
        };
        
        var arguments = new KernelArguments(settings)
        {
            ["material"] = "Steel",
            ["defectsJson"] = JsonSerializer.Serialize(defects)
        };

        // 4. Invoke
        var result = await _kernel.InvokeAsync(analysisFunction, arguments);
        return result.ToString();
    }
}