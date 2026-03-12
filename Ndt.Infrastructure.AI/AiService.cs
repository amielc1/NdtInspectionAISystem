using System.Reflection;
using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Google;
using Ndt.Domain;
using Ndt.Infrastructure.AI.Plugins; 
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.PromptTemplates.Handlebars;
using Microsoft.SemanticKernel.Planning.Handlebars;
using Microsoft.SemanticKernel.Plugins.Core;

namespace Ndt.Infrastructure.AI;

public class AiService  : IAiAnalysisService
{
    public event Action<List<Defect>>? DefectsDetected;
    
    private readonly Kernel _kernel;
    private readonly IDocumentMemoryService _memoryService;
    private readonly NdtVisionPlugin _visionPlugin;
    private ChatHistory? _chatHistory;

    public Func<string, Task<bool>>? ToolCallConfirmationAsync { get; set; }

    public AiService(Kernel kernel, IImageProcessor imageProcessor, IDocumentMemoryService memoryService)
    {
        _kernel = kernel;
        _memoryService = memoryService;
        // Manual instantiation of the plugin to keep a reference to the image state
        _visionPlugin = new NdtVisionPlugin(imageProcessor);
        _visionPlugin.DefectsDetected += defects => DefectsDetected?.Invoke(defects);
        
        _kernel.Plugins.AddFromObject(_visionPlugin, "WeldVision");
        _kernel.Plugins.AddFromType<NdtStandardsPlugin>("StandardsProvider");
        _kernel.Plugins.AddFromType<NdtReportingPlugin>("ReportingProvider");
        _kernel.Plugins.AddFromType<ConversationSummaryPlugin>("ConversationSummaryPlugin");
    }
    
    private async Task InitializeChatHistoryAsync()
    {
        if (_chatHistory != null) return;

        var promptTemplate = await LoadEmbeddedPromptAsync("Ndt.Infrastructure.AI.Prompts.WeldAnalysis.skprompt.txt");

        // Render the template to resolve variables like {{$material}}
        // If we don't render it, the AI sees raw template tags which prevents tool calling.
        var factory = new KernelPromptTemplateFactory();
        var template = factory.Create(new PromptTemplateConfig(promptTemplate));
        var renderedPrompt = await template.RenderAsync(_kernel, new KernelArguments
        {
            ["material"] = "Steel"
        });

        _chatHistory = new ChatHistory();
        _chatHistory.AddSystemMessage(renderedPrompt);
    }

    public async Task<string> AskQuestionAboutImageAsync(byte[] image, string userQuestion, Rectangle? roi = null)
    {
        // 1. Update the plugin with the latest image and ROI from the UI
        _visionPlugin.CurrentImage = image;
        if (roi != null)
        {
            _visionPlugin.RoiX = roi.X;
            _visionPlugin.RoiY = roi.Y;
            _visionPlugin.RoiWidth = roi.Width;
            _visionPlugin.RoiHeight = roi.Height;
        }
        else
        {
            _visionPlugin.RoiX = _visionPlugin.RoiY = _visionPlugin.RoiWidth = _visionPlugin.RoiHeight = 0;
        }

        var chat = _kernel.GetRequiredService<IChatCompletionService>();
        
        // 2. Setup the "Thinking" environment 
        var settings = new GeminiPromptExecutionSettings 
        { 
            ToolCallBehavior = GeminiToolCallBehavior.AutoInvokeKernelFunctions
        };

        await InitializeChatHistoryAsync();

        _chatHistory!.AddUserMessage(userQuestion);

        // 3. The AI will call DetectDefects() and CheckSafetyThreshold() automatically if needed
        var result = await chat.GetChatMessageContentAsync(_chatHistory, settings, _kernel);
        
        // Add the AI's response to history to maintain context
        _chatHistory.Add(result);
        
        return result.ToString();
    }

    public async Task<string> AskQuestionAsync(string userQuestion)
    {
        var chat = _kernel.GetRequiredService<IChatCompletionService>();
        
        var settings = new GeminiPromptExecutionSettings 
        { 
            ToolCallBehavior = GeminiToolCallBehavior.AutoInvokeKernelFunctions
        };

        await InitializeChatHistoryAsync();

        _chatHistory!.AddUserMessage(userQuestion);

        var result = await chat.GetChatMessageContentAsync(_chatHistory, settings, _kernel);
        
        _chatHistory.Add(result);
        
        return result.ToString();
    }

    public async Task<string> AskQuestionWithManualToolCallAsync(string userQuestion, Rectangle? roi = null)
    {
        // Although not using CurrentImage directly here, we should ensure ROI is consistent if tool is called
        if (roi != null)
        {
            _visionPlugin.RoiX = roi.X;
            _visionPlugin.RoiY = roi.Y;
            _visionPlugin.RoiWidth = roi.Width;
            _visionPlugin.RoiHeight = roi.Height;
        }
        else
        {
            _visionPlugin.RoiX = _visionPlugin.RoiY = _visionPlugin.RoiWidth = _visionPlugin.RoiHeight = 0;
        }

        var chat = _kernel.GetRequiredService<IChatCompletionService>();
        
        var settings = new GeminiPromptExecutionSettings 
        { 
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() 
        };

        await InitializeChatHistoryAsync();

        _chatHistory!.AddUserMessage(userQuestion);

        // Register the new filter into the Kernel BEFORE making the chat request
        _kernel.FunctionInvocationFilters.Add(new HumanApprovalFilter(AskUserForConfirmation));

        var result = await chat.GetChatMessageContentAsync(_chatHistory, settings, _kernel);
        
        _chatHistory.Add(result);
        return result.ToString();
    }

    private async Task<bool> AskUserForConfirmation(string message)
    {
        if (ToolCallConfirmationAsync != null)
        {
            return await ToolCallConfirmationAsync(message);
        }
        return true;
    }
    
    public async Task<string> AnalyzeImageAsync(byte[] image, List<Defect> defects)
    {
        // 1. Load the prompt from Embedded Resources
        var promptTemplate = await LoadEmbeddedPromptAsync("Ndt.Infrastructure.AI.Prompts.WeldAnalysis.skprompt.txt");

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

    public async Task<string> AnalyzeWithHandlebarsAsync(byte[] image, string material, Rectangle? roi = null)
    {
        // 1. Set the image and ROI in the plugin context
        _visionPlugin.CurrentImage = image;
        if (roi != null)
        {
            _visionPlugin.RoiX = roi.X;
            _visionPlugin.RoiY = roi.Y;
            _visionPlugin.RoiWidth = roi.Width;
            _visionPlugin.RoiHeight = roi.Height;
        }
        else
        {
            _visionPlugin.RoiX = _visionPlugin.RoiY = _visionPlugin.RoiWidth = _visionPlugin.RoiHeight = 0;
        }

        // 2. Load the Handlebars template
        var template = await LoadEmbeddedPromptAsync("Ndt.Infrastructure.AI.Prompts.WeldAnalysis.analysis.handlebars");
        var factory = new HandlebarsPromptTemplateFactory();
        var config = new PromptTemplateConfig()
        {
            Template = template,
            TemplateFormat = "handlebars",
            Name = "HandlebarsAnalysis",
        };
        // 3. Create the function using Handlebars template engine
        // var handlebarsFunction = _kernel.CreateFunctionFromPrompt(
        //     config, factory
        // );

        _kernel.FunctionInvocationFilters.Add(new HumanApprovalFilter(AskUserForConfirmation));

        // 3. Setup tool calling settings
        var settings = new GeminiPromptExecutionSettings 
        { 
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() 
        };

        // 4. Invoke with parameters
        var arguments = new KernelArguments(settings)
        {
            ["material"] = material
        };

        // Render the prompt with plugins registered in the kernel
        var promptTemplate = factory.Create(config);
        string? renderedPrompt = await promptTemplate.RenderAsync(_kernel, arguments);
        
        if (string.IsNullOrEmpty(renderedPrompt))
        {
            return "Error: Rendered prompt is empty or null. Check if the template contains valid Handlebars syntax and all plugin functions are available.";
        }
        
        // Final invoke using the fully rendered prompt
        var result = await _kernel.InvokePromptAsync(renderedPrompt, arguments);
        return result.ToString();
    }

    public async Task<string> GetDocumentInsightAsync(string documentText, string insightType)
    {
        string functionName = insightType switch
        {
            "Summary" => "SummarizeConversation",
            "ActionItems" => "GetConversationActionItems",
            "Topics" => "GetConversationTopics",
            _ => throw new ArgumentException($"Invalid insight type: {insightType}", nameof(insightType))
        };

        var arguments = new KernelArguments
        {
            ["input"] = documentText
        };

        var result = await _kernel.InvokeAsync("ConversationSummaryPlugin", functionName, arguments);
        return result.ToString();
    }

    public async Task<string> AskQuestionWithRagAsync(string userQuestion)
    {
        // 1. Retrieve Context
        var context = await _memoryService.SearchRelevantContextAsync("NDT_Docs", userQuestion);

        // 2. Load the prompt from Embedded Resources
        var promptTemplate = await LoadEmbeddedPromptAsync("Ndt.Infrastructure.AI.Prompts.RAGPromptTemplate.txt");

        // 3. Create Function
        var ragFunction = _kernel.CreateFunctionFromPrompt(promptTemplate);

        // 4. Set Arguments
        var arguments = new KernelArguments
        {
            ["context"] = context,
            ["question"] = userQuestion
        };

        // 5. Invoke and Return
        var result = await _kernel.InvokeAsync(ragFunction, arguments);
        return result.ToString();
    }

    private async Task<string> LoadEmbeddedPromptAsync(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            throw new InvalidOperationException($"Could not find embedded resource: {resourceName}");
        }
        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync();
    }
}