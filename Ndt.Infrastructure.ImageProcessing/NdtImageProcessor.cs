using Ndt.Domain;
using OpenCvSharp;

namespace Ndt.Infrastructure.ImageProcessing;

public class NdtImageProcessor : IImageProcessor
{
    public byte[] ApplyHistogramStretching(byte[] inputImage, bool equalized)
    {
        using var mat = Cv2.ImDecode(inputImage, ImreadModes.Color);
        using var processed = ApplyHistogramStretching(mat, equalized);
        return processed.ToBytes();
    }

    public List<Defect> DetectDefects(byte[] inputImage, Ndt.Domain.Rectangle roi)
    {
        using var mat = Cv2.ImDecode(inputImage, ImreadModes.Color);
        var cvRoi = new OpenCvSharp.Rect(roi.X, roi.Y, roi.Width, roi.Height);
        return DetectDefects(mat, cvRoi);
    }

    public byte[] GenerateResultImage(byte[] inputImage, List<Defect> defects)
    {
        using var mat = Cv2.ImDecode(inputImage, ImreadModes.Color);
        using var result = GenerateResultImage(mat, defects);
        return result.ToBytes();
    }

    // OpenCV-specific methods as requested
    public Mat ApplyHistogramStretching(Mat input, bool equalized)
    {
        var result = new Mat();
        if (input.Channels() > 1)
        {
            // For color images, we can process each channel or convert to YCrCb
            using var ycrcb = new Mat();
            Cv2.CvtColor(input, ycrcb, ColorConversionCodes.BGR2YCrCb);
            Mat[] channels = Cv2.Split(ycrcb);

            if (equalized)
            {
                Cv2.EqualizeHist(channels[0], channels[0]);
            }
            else
            {
                Cv2.Normalize(channels[0], channels[0], 0, 255, NormTypes.MinMax);
            }

            Cv2.Merge(channels, ycrcb);
            Cv2.CvtColor(ycrcb, result, ColorConversionCodes.YCrCb2BGR);
        }
        else
        {
            if (equalized)
            {
                Cv2.EqualizeHist(input, result);
            }
            else
            {
                Cv2.Normalize(input, result, 0, 255, NormTypes.MinMax);
            }
        }
        return result;
    }

    public List<Defect> DetectDefects(Mat input, OpenCvSharp.Rect roi)
    {
        var defects = new List<Defect>();
        
        // Ensure ROI is valid
        var validRoi = roi;
        if (roi.Width <= 0 || roi.Height <= 0)
            validRoi = new OpenCvSharp.Rect(0, 0, input.Width, input.Height);
        
        using var subMat = new Mat(input, validRoi);
        using var gray = new Mat();
        Cv2.CvtColor(subMat, gray, ColorConversionCodes.BGR2GRAY);
        
        using var thresh = new Mat();
        Cv2.Threshold(gray, thresh, 0, 255, ThresholdTypes.BinaryInv | ThresholdTypes.Otsu);
        
        Cv2.FindContours(thresh, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
        
        int id = 1;
        foreach (var contour in contours)
        {
            double area = Cv2.ContourArea(contour);
            if (area < 10) continue; // Filter small noise

            var rect = Cv2.BoundingRect(contour);
            var moments = Cv2.Moments(contour);
            var center = new Ndt.Domain.Point(
                (int)(moments.M10 / moments.M00) + validRoi.X,
                (int)(moments.M01 / moments.M00) + validRoi.Y
            );

            defects.Add(new Defect(
                id++,
                DefectType.Porosity, // Simplified, would need more logic for classification
                area,
                center,
                new Ndt.Domain.Rectangle(rect.X + validRoi.X, rect.Y + validRoi.Y, rect.Width, rect.Height)
            ));
        }

        return defects;
    }

    public Mat GenerateResultImage(Mat input, List<Defect> defects)
    {
        var result = input.Clone();
        foreach (var defect in defects)
        {
            var rect = new OpenCvSharp.Rect(
                defect.BoundingRect.X, 
                defect.BoundingRect.Y, 
                defect.BoundingRect.Width, 
                defect.BoundingRect.Height
            );
            
            Cv2.Rectangle(result, rect, Scalar.Red, 2);
            Cv2.PutText(result, $"ID:{defect.Id} Area:{defect.Area:F1}", 
                new OpenCvSharp.Point(rect.X, rect.Y - 5), 
                HersheyFonts.HersheySimplex, 0.5, Scalar.Yellow, 1);
        }
        return result;
    }
}
