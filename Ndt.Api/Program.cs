using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.Google;
using Microsoft.SemanticKernel.Connectors.Chroma;
using Microsoft.SemanticKernel.Memory;
using Ndt.Domain;
using Ndt.Domain.Interfaces;
using Ndt.Domain.Models;
using Ndt.Infrastructure.AI;
using Ndt.Infrastructure.ImageProcessing;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddUserSecrets<Program>();

// --- Configure Dependency Injection ---

// Read settings 
var aiSettings = builder.Configuration.GetSection("AiSettings");
var apiKey = aiSettings["GEMINI_API_KEY"];
var modelId = aiSettings["ModelId"];
var embeddingModelId = aiSettings["EmbeddingModelId"];
var chromaEndpoint = aiSettings["ChromaEndpoint"]!;

// Initializing the Semantic Text Memory with a persistent Chroma memory store.

// 1. Register Semantic Kernel instance
builder.Services.AddTransient<Kernel>(sp =>
{
    var kernelBuilder = Kernel.CreateBuilder();
    kernelBuilder.AddGoogleAIGeminiChatCompletion(modelId, apiKey!);
    return kernelBuilder.Build();
});

// 2. Configure ISemanticTextMemory using MemoryBuilder
builder.Services.AddSingleton<ISemanticTextMemory>(sp =>
{
    return new MemoryBuilder()
        .WithGoogleAITextEmbeddingGeneration(embeddingModelId, apiKey!)
        .WithChromaMemoryStore(chromaEndpoint)
        .Build();
});

// 3. Register IVectorDbManagerService mapping to VectorDbManagerService
builder.Services.AddTransient<IVectorDbManagerService, VectorDbManagerService>();

// 4. Register IDocumentMemoryService mapping to DocumentMemoryService
builder.Services.AddTransient<IDocumentMemoryService, DocumentMemoryService>();

// 5. Register IAiAnalysisService mapping to AiService
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
app.MapGet("/api/vector/collections", async (IVectorDbManagerService db) =>
    {
        try
        {
            var collections = await db.GetCollectionsAsync();
            return Results.Ok(new { Collections = collections });
        }
        catch (Exception ex) 
        { 
            return Results.Problem(ex.Message); 
        }
    })
    .WithName("GetVectorCollections");
// Vector Database CRUD Endpoints
app.MapPost("/api/vector/import", async (ImportDocRequest request, IVectorDbManagerService vectorService) =>
    {
        await vectorService.ImportDocumentAsync(request.CollectionName, request.DocumentName, request.Text, request.Year, request.Material);
        return Results.Ok(new { Message = $"Document {request.DocumentName} imported successfully." });
    })
    .WithName("ImportVectorDocument");

app.MapPost("/api/vector/search", async (SearchDocRequest request, IVectorDbManagerService vectorService) =>
    {
        var results = await vectorService.SearchByYearAsync(request.CollectionName, request.Query, request.TargetYear);
        return Results.Ok(new { Results = results });
    })
    .WithName("SearchVectorByYear");

app.MapDelete("/api/vector/delete/{collection}/{documentName}/{totalChunks:int}", async (string collection, string documentName, int totalChunks, IVectorDbManagerService vectorService) =>
    {
        await vectorService.DeleteDocumentAsync(collection, documentName, totalChunks);
        return Results.Ok(new { Message = $"Document {documentName} deleted successfully." });
    })
    .WithName("DeleteVectorDocument");

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
public record ImportDocRequest(string CollectionName, string DocumentName, string Text, int Year, string Material);

public record SearchDocRequest(string CollectionName, string Query, int TargetYear);

public record ImportRequest(string Text, string CollectionName);

public record AskRequest(string Question);