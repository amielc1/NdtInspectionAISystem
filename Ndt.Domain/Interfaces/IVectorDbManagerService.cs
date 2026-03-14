namespace Ndt.Domain.Interfaces;

public interface IVectorDbManagerService
{
    Task ImportDocumentAsync(string collectionName, string documentName, string text, int year, string material);
    Task<string> SearchByYearAsync(string collectionName, string query, int targetYear);
    Task DeleteDocumentAsync(string collectionName, string documentName, int totalChunks);
    Task<IList<string>> GetCollectionsAsync();
}