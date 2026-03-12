using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Text;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Ndt.Domain;

namespace Ndt.Infrastructure.AI;

/// <summary>
/// Service for handling document ingestion into vector storage and performing similarity search.
/// </summary>
public class DocumentMemoryService : IDocumentMemoryService
{
    private readonly ISemanticTextMemory _memory;

    public DocumentMemoryService()
    {
        // Initializing the Semantic Text Memory with a Volatile (in-memory) store.
        _memory = new MemoryBuilder()
            .WithMemoryStore(new VolatileMemoryStore())
            // PLACEHOLDER: You need to configure an embedding generation service here.
            // Example for OpenAI:
            // .WithOpenAITextEmbeddingGeneration("text-embedding-3-small", "YOUR_API_KEY")
            // Example for Google:
            // .WithGoogleTextEmbeddingGeneration("models/embedding-001", "YOUR_API_KEY")
            .Build();
    }

    /// <summary>
    /// Chunks the document text and imports it into the specified collection in the memory store.
    /// </summary>
    /// <param name="documentText">The raw text of the document.</param>
    /// <param name="collectionName">The name of the collection to store the document chunks in.</param>
    public async Task ImportDocumentAsync(string documentText, string collectionName)
    {
        // Split text into lines with a maximum of 40 tokens each.
        var lines = TextChunker.SplitPlainTextLines(documentText, 40);
        
        // Split lines into paragraphs with a maximum of 120 tokens and an overlap of 20 tokens.
        var paragraphs = TextChunker.SplitPlainTextParagraphs(lines, 120, 20);

        foreach (var chunk in paragraphs)
        {
            // Each chunk is saved with a unique ID.
            var uniqueId = Guid.NewGuid().ToString();
            await _memory.SaveInformationAsync(
                collection: collectionName,
                text: chunk,
                id: uniqueId);
        }
    }

    /// <summary>
    /// Searches for relevant context in the memory store based on a user query.
    /// </summary>
    /// <param name="collectionName">The name of the collection to search in.</param>
    /// <param name="userQuery">The query to search for.</param>
    /// <param name="limit">The maximum number of results to return.</param>
    /// <returns>A concatenated string of the most relevant context parts.</returns>
    public async Task<string> SearchRelevantContextAsync(string collectionName, string userQuery, int limit = 2)
    {
        // Search the specified collection for the user query.
        var results = _memory.SearchAsync(collectionName, userQuery, limit: limit);
        
        var contextParts = new List<string>();
        
        await foreach (var result in results)
        {
            // Extract the text from the metadata of each search result.
            if (!string.IsNullOrEmpty(result.Metadata.Text))
            {
                contextParts.Add(result.Metadata.Text);
            }
        }

        // Return the combined text parts separated by newlines.
        return string.Join(Environment.NewLine, contextParts);
    }
}
