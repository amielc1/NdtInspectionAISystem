using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ndt.Domain;
using System.IO;
using Microsoft.Win32;

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
            Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            try
            {
                DocumentContent = await File.ReadAllTextAsync(openFileDialog.FileName);
                StatusMessage = $"Loaded: {Path.GetFileName(openFileDialog.FileName)}";
                InsightResult = string.Empty;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading document: {ex.Message}";
            }
        }
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
