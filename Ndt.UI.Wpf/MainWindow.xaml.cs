using Ndt.UI.Wpf.ViewModels;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Ndt.UI.Wpf;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private Point _startPoint;
    private bool _isSelecting = false;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnImageMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            _startPoint = e.GetPosition(ImageCanvas);
            _isSelecting = true;
            
            Canvas.SetLeft(RoiRectangle, _startPoint.X);
            Canvas.SetTop(RoiRectangle, _startPoint.Y);
            RoiRectangle.Width = 0;
            RoiRectangle.Height = 0;
            RoiRectangle.Visibility = Visibility.Visible;
            
            ImageCanvas.CaptureMouse();
        }
    }

    private void OnImageMouseMove(object sender, MouseEventArgs e)
    {
        if (_isSelecting)
        {
            var currentPoint = e.GetPosition(ImageCanvas);
            
            var x = Math.Min(_startPoint.X, currentPoint.X);
            var y = Math.Min(_startPoint.Y, currentPoint.Y);
            var w = Math.Abs(_startPoint.X - currentPoint.X);
            var h = Math.Abs(_startPoint.Y - currentPoint.Y);

            Canvas.SetLeft(RoiRectangle, x);
            Canvas.SetTop(RoiRectangle, y);
            RoiRectangle.Width = w;
            RoiRectangle.Height = h;
        }
    }

    private void OnImageMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_isSelecting)
        {
            _isSelecting = false;
            ImageCanvas.ReleaseMouseCapture();

            UpdateRoiInViewModel();
        }
    }

    private void UpdateRoiInViewModel()
    {
        if (DataContext is MainViewModel vm && MainImage.Source is BitmapSource source)
        {
            // Calculate scale between displayed image and original source
            // Stretch="Uniform" logic
            double canvasWidth = ImageCanvas.ActualWidth;
            double canvasHeight = ImageCanvas.ActualHeight;
            double sourceWidth = source.PixelWidth;
            double sourceHeight = source.PixelHeight;

            double scaleX = canvasWidth / sourceWidth;
            double scaleY = canvasHeight / sourceHeight;
            double scale = Math.Min(scaleX, scaleY);

            double displayedWidth = sourceWidth * scale;
            double displayedHeight = sourceHeight * scale;

            double offsetX = (canvasWidth - displayedWidth) / 2;
            double offsetY = (canvasHeight - displayedHeight) / 2;

            double rectX = Canvas.GetLeft(RoiRectangle);
            double rectY = Canvas.GetTop(RoiRectangle);
            double rectW = RoiRectangle.Width;
            double rectH = RoiRectangle.Height;

            // Map canvas coordinates to image pixel coordinates
            double imageX = (rectX - offsetX) / scale;
            double imageY = (rectY - offsetY) / scale;
            double imageW = rectW / scale;
            double imageH = rectH / scale;

            // Clamp and update ViewModel
            vm.RoiX = (int)Math.Max(0, Math.Min(sourceWidth, imageX));
            vm.RoiY = (int)Math.Max(0, Math.Min(sourceHeight, imageY));
            vm.RoiWidth = (int)Math.Max(0, Math.Min(sourceWidth - vm.RoiX, imageW));
            vm.RoiHeight = (int)Math.Max(0, Math.Min(sourceHeight - vm.RoiY, imageH));

            Console.WriteLine($"[DEBUG_LOG] ROI updated: X={vm.RoiX}, Y={vm.RoiY}, W={vm.RoiWidth}, H={vm.RoiHeight}");
        }
    }
}