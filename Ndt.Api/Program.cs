using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Google;
using Ndt.Domain;
using Ndt.Infrastructure.AI;
using Ndt.Infrastructure.ImageProcessing;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// --- Configure Dependency Injection ---

// Read settings
var aiSettings = builder.Configuration.GetSection("AiSettings");
var apiKey = aiSettings["GEMINI_API_KEY"];
var modelId = aiSettings["ModelId"] ?? "gemini-2.0-flash";

// 1. Register Semantic Kernel instance
builder.Services.AddTransient<Kernel>(sp => 
{
    var kernelBuilder = Kernel.CreateBuilder();
    kernelBuilder.AddGoogleAIGeminiChatCompletion(modelId, apiKey!);
    return kernelBuilder.Build();
});

// 2. Register IDocumentMemoryService mapping to DocumentMemoryService
builder.Services.AddTransient<IDocumentMemoryService, DocumentMemoryService>();

// 3. Register IAiAnalysisService mapping to AiService
builder.Services.AddTransient<IAiAnalysisService, AiService>();

// Dependency for AiService
builder.Services.AddTransient<IImageProcessor, NdtImageProcessor>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// --- Configuration ---

if (true)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// --- Minimal API Endpoints ---

// POST /api/knowledge/import
// Accepts Text and CollectionName
app.MapPost("/api/knowledge/import", async (ImportRequest request, IDocumentMemoryService memoryService) =>
    {
        try
        {
            await memoryService.ImportDocumentAsync(request.Text, request.CollectionName);
            return Results.Ok(new
                { Message = "Document imported successfully into collection: " + request.CollectionName });
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message);
        }
    })
    .WithName("ImportKnowledge");
// POST /api/knowledge/ask
// Accepts Question and calls AskQuestionWithRagAsync
app.MapPost("/api/knowledge/ask", async (AskRequest request, IAiAnalysisService aiService) =>
    {
        try
        {
            var answer = await aiService.AskQuestionWithRagAsync(request.Question);
            return Results.Ok(new { Question = request.Question, Answer = answer });
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message);
        }
    })
    .WithName("AskKnowledge");

app.Run();

// --- DTOs (Records) ---
public record ImportRequest(string Text, string CollectionName);
public record AskRequest(string Question);
