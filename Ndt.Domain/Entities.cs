namespace Ndt.Domain;

public record Point(int X, int Y);
public record Rectangle(int X, int Y, int Width, int Height);

public record Defect(
    int Id,
    DefectType Type,
    double Area,
    Point CenterPoint,
    Rectangle BoundingRect
);

public record ProcessingResult(
    byte[] ResultImage,
    List<Defect> Defects,
    string AnalysisSummary
);
