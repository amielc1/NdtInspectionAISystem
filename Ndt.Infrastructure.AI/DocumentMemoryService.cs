using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Text;
using Microsoft.SemanticKernel.Connectors.Google;
using Microsoft.SemanticKernel.Connectors.Chroma;
using Microsoft.Extensions.Configuration;
using Ndt.Domain;

namespace Ndt.Infrastructure.AI;

/// <summary>
/// Service for handling document ingestion into vector storage and performing similarity search.
/// </summary>
public class DocumentMemoryService : IDocumentMemoryService
{
    private readonly ISemanticTextMemory _memory;

    public DocumentMemoryService(IConfiguration configuration)
    {
        var apiKey = configuration["AiSettings:GEMINI_API_KEY"];
        var embeddingModelId = configuration["AiSettings:EmbeddingModelId"] ?? "gemini-embedding-001";
        var chromaEndpoint = configuration["AiSettings:ChromaEndpoint"]!;

        // Initializing the Semantic Text Memory with a persistent Chroma memory store.
        _memory = new MemoryBuilder()
            .WithMemoryStore(new ChromaMemoryStore(chromaEndpoint))
            .WithGoogleAITextEmbeddingGeneration(modelId: embeddingModelId, apiKey: apiKey!)
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

        var results = _memory.SearchAsync(collectionName, userQuery, limit: limit, minRelevanceScore: 0.55); 
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
 
    public async Task DemoChunkingAndMemoryAsync()
    {
        // 1. הטקסט המקורי שלנו (נגיד שקראנו אותו עכשיו מקובץ)
        string documentText = @"
ריתוך בקרן אלקטרונים (EBW) הוא תהליך ריתוך היתוך.
בתהליך זה, אלומת אלקטרונים במהירות גבוהה מופעלת על החומרים המרותכים.
האנרגיה הקינטית של האלקטרונים הופכת לחום בעת הפגיעה בחומר העבודה.
החום גורם להמסת המתכת וליצירת החיבור.
התהליך מתבצע לרוב בתנאי ריק (ואקום) כדי למנוע פיזור של אלומת האלקטרונים.
";

        Console.WriteLine("=== מתחילים תהליך חיתוך (Chunking) ===");

        // 2. חלוקה לשורות: נגביל כל שורה למקסימום 15 טוקנים (כ-10 מילים).
        // ה-Chunker מספיק חכם לא לחתוך מילה באמצע, אלא למצוא רווח או נקודה.
        var lines = TextChunker.SplitPlainTextLines(documentText, maxTokensPerLine: 15);

        Console.WriteLine($"\nהטקסט חולק ל-{lines.Count} שורות בסיסיות.");

        // 3. חלוקה לפסקאות (עם חפיפה!): מקסימום 30 טוקנים לפסקה, עם 10 טוקנים שחופפים (Overlap).
        var paragraphs = TextChunker.SplitPlainTextParagraphs(
            lines,
            maxTokensPerParagraph: 30,
            overlapTokens: 10);

        Console.WriteLine($"הטקסט אוגד ל-{paragraphs.Count} פסקאות חכמות (Chunks).\n");

        // 4. שמירה בזיכרון (Ingestion לתוך ה-Vector Database)
        string collectionName = "Welding_Knowledge_Base";

        for (int i = 0; i < paragraphs.Count; i++)
        {
            string chunkText = paragraphs[i];
            string uniqueId = $"chunk_ebw_{i}"; // מזהה ייחודי לכל חתיכה

            Console.WriteLine($"[שומר את חתיכה {i} בזיכרון...]");
            Console.WriteLine($"טקסט: {chunkText}");
            Console.WriteLine("- - - - - - - - - - - - - - -");

            // פקודת הקסם שמייצרת את הוקטור ושומרת במסד הנתונים!
            await _memory.SaveInformationAsync(
                collection: collectionName,
                text: chunkText,
                id: uniqueId
            );
        }

        Console.WriteLine("\n=== סיום בהצלחה! המידע הוכנס לזיכרון הווקטורי ===");
    }
}
