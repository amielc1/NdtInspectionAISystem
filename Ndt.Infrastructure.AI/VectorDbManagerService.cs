using System.Text.Json;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Text;
using Ndt.Domain.Interfaces;
using Ndt.Domain.Models;

namespace Ndt.Infrastructure.AI;

/// <summary>
/// Implementation of IVectorDbManagerService using Semantic Kernel's ISemanticTextMemory.
/// </summary>
public class VectorDbManagerService : IVectorDbManagerService
{
    private readonly ISemanticTextMemory _memory;

    public VectorDbManagerService(ISemanticTextMemory memory)
    {
        _memory = memory;
    }
    public async Task<IList<string>> GetCollectionsAsync()
    {
        // שליפת כל הקולקציות מ-Chroma DB
        var collections = await _memory.GetCollectionsAsync();
        return collections;
    }
    
    /// <summary>
    /// Imports a document into the vector database by chunking it and saving each chunk with metadata.
    /// </summary>
    public async Task ImportDocumentAsync(string collectionName, string documentName, string text, int year, string material)
    {
        // Use TextChunker to split the text into paragraphs.
        // We first split by lines then by paragraphs to ensure good quality chunks.
        var lines = TextChunker.SplitPlainTextLines(text, 128);
        var paragraphs = TextChunker.SplitPlainTextParagraphs(lines, 1024);

        for (int i = 0; i < paragraphs.Count; i++)
        {
            // Generate predictable IDs formatted as "{documentName}_chunk_{i}"
            string chunkId = $"{documentName}_chunk_{i}";

            // Create metadata for the chunk
            var metadata = new DocumentMetadata
            {
                DocumentName = documentName,
                DocumentType = "Document", // Defaulting as it's not in the method signature but in the model
                Year = year,
                Material = material,
                ChunkIndex = i,
                TotalChunks = paragraphs.Count
            };

            // Serialize metadata to JSON string
            string metadataJson = JsonSerializer.Serialize(metadata);

            // Save information to the vector memory
            await _memory.SaveInformationAsync(
                collection: collectionName,
                text: paragraphs[i],
                id: chunkId,
                description: $"Chunk {i} of {documentName}",
                additionalMetadata: metadataJson);
        }
    }

    /// <summary>
    /// Searches for documents in a collection and filters them by a specific year.
    /// </summary>
    public async Task<string> SearchByYearAsync(string collectionName, string query, int targetYear)
    {
        // Search the memory with a minimum relevance score of 0.50
        var results = _memory.SearchAsync(collectionName, query, limit: 10, minRelevanceScore: 0.50);

        var matchedContexts = new List<string>();

        await foreach (var result in results)
        {
            try
            {
                // Deserialize the JSON metadata
                var metadata = JsonSerializer.Deserialize<DocumentMetadata>(result.Metadata.AdditionalMetadata);

                // Filter ONLY the results where the Year matches the targetYear
                if (metadata != null && metadata.Year == targetYear)
                {
                    matchedContexts.Add(result.Metadata.Text);
                }
            }
            catch (JsonException)
            {
                // Skip results with invalid metadata
                continue;
            }
        }

        // Return a combined string of the matched contexts
        return string.Join("\n\n", matchedContexts);
    }

    /// <summary>
    /// Deletes a document from the vector database by removing all its chunks.
    /// </summary>
    public async Task DeleteDocumentAsync(string collectionName, string documentName, int totalChunks)
    {
        // Loop from 0 to totalChunks - 1 to recreate and remove each chunk
        for (int i = 0; i < totalChunks; i++)
        {
            string chunkId = $"{documentName}_chunk_{i}";
            await _memory.RemoveAsync(collectionName, chunkId);
        }
    }
}
