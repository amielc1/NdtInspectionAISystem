using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ndt.Domain;
using System.Collections.ObjectModel;
using System.Windows.Media.Imaging;
using System.IO;

namespace Ndt.UI.Wpf.ViewModels;

public partial class MainViewModel(IImageProcessor imageProcessor, IAiAnalysisService aiService) : ObservableObject
{
    [ObservableProperty]
    private byte[]? _originalImage;

    [ObservableProperty]
    private BitmapSource? _displayImage;

    [ObservableProperty]
    private ObservableCollection<Defect> _defects = new();

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private string _analysisSummary = string.Empty;

    [ObservableProperty]
    private ObservableCollection<ChatMessage> _chatHistory = new();

    [ObservableProperty]
    private string _userQuestion = string.Empty;

    [ObservableProperty]
    private bool _useImageInChat = true;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _sendWithImage = true;

    [ObservableProperty]
    private int _roiX = 0;

    [ObservableProperty]
    private int _roiY = 0;

    [ObservableProperty]
    private int _roiWidth = 0;

    [ObservableProperty]
    private int _roiHeight = 0;

    [RelayCommand]
    private void LoadImage()
    {
        var openFileDialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Image files (*.png;*.jpeg;*.jpg;*.bmp)|*.png;*.jpeg;*.jpg;*.bmp|All files (*.*)|*.*"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            try
            {
                var imageBytes = File.ReadAllBytes(openFileDialog.FileName);
                OriginalImage = imageBytes;
                UpdateDisplayImage(imageBytes);
                ChatHistory.Clear();
                AnalysisSummary = string.Empty;
                StatusText = $"Loaded: {Path.GetFileName(openFileDialog.FileName)}";
            }
            catch (Exception ex)
            {
                StatusText = $"Error loading image: {ex.Message}";
            }
        }
    }

    [RelayCommand]
    private void ApplyLinearStretch()
    {
        if (OriginalImage == null) return;
        var processed = imageProcessor.ApplyHistogramStretching(OriginalImage, false);
        UpdateDisplayImage(processed);
        StatusText = "Linear stretch applied.";
    }

    [RelayCommand]
    private void ApplyEqualizedStretch()
    {
        if (OriginalImage == null) return;
        var processed = imageProcessor.ApplyHistogramStretching(OriginalImage, true);
        UpdateDisplayImage(processed);
        StatusText = "Equalized stretch applied.";
    }

    [RelayCommand]
    private async Task AnalyzeManual()
    {
        if (OriginalImage == null) return;
        
        IsBusy = true;
        StatusText = "Analyzing image manually...";

        try
        {
            var roi = new Domain.Rectangle(RoiX, RoiY, RoiWidth, RoiHeight);
            var detectedDefects = imageProcessor.DetectDefects(OriginalImage, roi);
            
            Defects.Clear();
            foreach (var defect in detectedDefects)
            {
                Defects.Add(defect);
            }

            var resultImageBytes = imageProcessor.GenerateResultImage(OriginalImage, detectedDefects);
            UpdateDisplayImage(resultImageBytes);
            
            //AnalysisSummary = await aiService.AnalyzeImageAsync(OriginalImage, detectedDefects);
            //ChatHistory.Add(new ChatMessage(AnalysisSummary, MessageSender.AI, DateTime.Now));
            StatusText = $"Analysis complete. {Defects.Count} defects found.";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            AnalysisSummary = $"An error occurred: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Ask()
    {
        UserQuestion = string.Empty;
        StatusText = "Please input your question and press Enter.";
    }

    [RelayCommand]
    private async Task SendQuestion()
    {
        if (OriginalImage == null || string.IsNullOrWhiteSpace(UserQuestion)) return;
        
        var question = UserQuestion;
        ChatHistory.Add(new ChatMessage(question, MessageSender.User, DateTime.Now));
        IsBusy = true;
        StatusText = "Sending question to AI...";
        UserQuestion = string.Empty;
        try
        {
            string response;
            if (UseImageInChat)
            {
                response = await aiService.AskQuestionAboutImageAsync(OriginalImage, question);
            }
            else
            {
                response = await aiService.AskQuestionAsync(question);
            }
            
            AnalysisSummary = response;
            ChatHistory.Add(new ChatMessage(response, MessageSender.AI, DateTime.Now));
            StatusText = "AI Response received.";
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            AnalysisSummary = $"An error occurred: {ex.Message}";
            ChatHistory.Add(new ChatMessage($"Error: {ex.Message}", MessageSender.AI, DateTime.Now));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OnDefectsDetected(List<Defect> defects)
    {
        App.Current.Dispatcher.Invoke(() =>
        {
            Defects.Clear();
            foreach (var defect in defects)
            {
                Defects.Add(defect);
            }
            
            // Optionally update the display image with detected defects
            var resultImageBytes = imageProcessor.GenerateResultImage(OriginalImage!, defects);
            UpdateDisplayImage(resultImageBytes);
        });
    }

    private void UpdateDisplayImage(byte[] imageBytes)
    {
        using var ms = new MemoryStream(imageBytes);
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.StreamSource = ms;
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.EndInit();
        bitmap.Freeze();
        DisplayImage = bitmap;
    }

    private byte[] CreatePlaceholderImage()
    {
        // Simple 400x400 PNG placeholder
        using var ms = new MemoryStream();
        var pixelData = new byte[400 * 400];
        for (int i = 0; i < pixelData.Length; i++) pixelData[i] = 128; // Gray
        
        var bitmap = BitmapSource.Create(400, 400, 96, 96, System.Windows.Media.PixelFormats.Gray8, null, pixelData, 400);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        encoder.Save(ms);
        return ms.ToArray();
    }
}
