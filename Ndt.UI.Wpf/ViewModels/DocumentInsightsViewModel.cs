using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ndt.Domain;
using System.IO;
using Microsoft.Win32;
using UglyToad.PdfPig;
using System.Text;

namespace Ndt.UI.Wpf.ViewModels;

public partial class DocumentInsightsViewModel : ObservableObject
{
    private readonly IAiAnalysisService _aiService;

    [ObservableProperty]
    private string _documentContent = string.Empty;

    [ObservableProperty]
    private string _insightResult = string.Empty;

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private string _selectedInsightType = "Summary"; // Default

    [ObservableProperty]
    private string _statusMessage = "Ready to upload document.";

    public DocumentInsightsViewModel(IAiAnalysisService aiService)
    {
        _aiService = aiService;
    }

    [RelayCommand]
    private async Task UploadDocument()
    {
        var openFileDialog = new OpenFileDialog
        {
            Filter = "Document files (*.txt, *.pdf)|*.txt;*.pdf|Text files (*.txt)|*.txt|PDF files (*.pdf)|*.pdf|All files (*.*)|*.*"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            try
            {
                var filePath = openFileDialog.FileName;
                var extension = Path.GetExtension(filePath).ToLowerInvariant();

                if (extension == ".pdf")
                {
                    DocumentContent = await Task.Run(() => ExtractTextFromPdf(filePath));
                }
                else
                {
                    DocumentContent = await File.ReadAllTextAsync(filePath);
                }

                StatusMessage = $"Loaded: {Path.GetFileName(filePath)}";
                InsightResult = string.Empty;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading document: {ex.Message}";
            }
        }
    }

    private string ExtractTextFromPdf(string filePath)
    {
        var sb = new StringBuilder();
        using (var document = PdfDocument.Open(filePath))
        {
            foreach (var page in document.GetPages())
            {
                sb.AppendLine(page.Text);
            }
        }
        return sb.ToString();
    }

    [RelayCommand]
    private async Task AnalyzeDocument()
    {
        if (string.IsNullOrWhiteSpace(DocumentContent))
        {
            StatusMessage = "Please upload a document first.";
            return;
        }

        IsProcessing = true;
        StatusMessage = $"Analyzing technical document for {SelectedInsightType}...";
        InsightResult = string.Empty;

        try
        {
            InsightResult = await _aiService.GetDocumentInsightAsync(DocumentContent, SelectedInsightType);
            StatusMessage = "Analysis complete.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error during analysis: {ex.Message}";
            InsightResult = $"An error occurred: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
        }
    }
}
