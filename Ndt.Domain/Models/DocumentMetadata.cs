namespace Ndt.Domain.Models;

public record DocumentMetadata
{
    public string DocumentName { get; init; } = string.Empty;
    public string DocumentType { get; init; } = string.Empty;
    public int Year { get; init; }
    public string Material { get; init; } = string.Empty;
    public int ChunkIndex { get; init; }
    public int TotalChunks { get; init; }
}