using BlackHoleRayTracer.Camera;
using BlackHoleRayTracer.Configuration;

using SkiaSharp;

using System.Runtime.InteropServices;

namespace BlackHoleRayTracer.Shader;

/// <summary>
/// Draws a small top-down (horizontal-plane) trajectory plot — a black central
/// body plus the path of an array of spherical-coordinate samples — into a
/// corner of an in-memory RGB image buffer.
/// </summary>
public static class TrajectoryPlot
{
  private const float BoxSize = 180f;
  private const float Margin = 20f;
  private const float Padding = 10f;
  private const float PointMarkerRadius = 2f;

  private static readonly SKColor CurrentPositionColor = SKColors.Red;
  private const float CurrentPositionRadius = 5f;

  private static readonly SKColor BackgroundColor = new SKColor(50, 70, 70, 190);
  private static readonly SKColor CompletedColor = new SKColor(240, 240, 240, 150);
  private static readonly SKColor RemainingColor = new SKColor(240, 240, 240, 150);

  private readonly record struct Position(float X, float Y);

  public static void Draw(byte[] rgb, CameraKeyframe[] trajectory, double progress)
  {
    int width = Config.Camera.Width;
    int height = Config.Camera.Height;
    int n = width * height;
    var rgba = new byte[n * 4];
    for (int i = 0, j = 0; i < n * 3; i += 3, j += 4)
    {
      rgba[j] = rgb[i];
      rgba[j + 1] = rgb[i + 1];
      rgba[j + 2] = rgb[i + 2];
      rgba[j + 3] = 255;
    }

    var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque);
    using var bmp = new SKBitmap(info);
    Marshal.Copy(rgba, 0, bmp.GetPixels(), rgba.Length);

    using (var canvas = new SKCanvas(bmp))
    {
      DrawPlot(canvas, width, height, trajectory, progress);
      canvas.Flush();
    }

    Marshal.Copy(bmp.GetPixels(), rgba, 0, rgba.Length);
    for (int i = 0, j = 0; i < n * 3; i += 3, j += 4)
    {
      rgb[i] = rgba[j];
      rgb[i + 1] = rgba[j + 1];
      rgb[i + 2] = rgba[j + 2];
    }
  }

  private static void DrawPlot(SKCanvas canvas, int imageWidth, int imageHeight, CameraKeyframe[] trajectory, double progress)
  {
    // Bottom-right corner, so it doesn't collide with the Legend (top-left).
    float boxLeft = imageWidth - BoxSize - Margin;
    float boxTop = imageHeight - BoxSize - Margin;
    var boxRect = SKRect.Create(boxLeft, boxTop, BoxSize, BoxSize);

    progress = Math.Clamp(progress, 0, 1);

    using var background = new SKPaint { Color = BackgroundColor };
    using var border = new SKPaint { Color = SKColors.White, IsStroke = true, StrokeWidth = 1, IsAntialias = true };
    using var centralBodyPaint = new SKPaint { Color = SKColors.Black, IsAntialias = true };

    using var completedLinePaint = new SKPaint
    {
      Color = CompletedColor,
      IsAntialias = true,
      IsStroke = true,
      StrokeWidth = 1.5f
    };
    using var completedPointPaint = new SKPaint { Color = CompletedColor, IsAntialias = true };

    using var remainingLinePaint = new SKPaint
    {
      Color = RemainingColor,
      IsAntialias = true,
      IsStroke = true,
      StrokeWidth = 1.5f
    };
    using var remainingPointPaint = new SKPaint { Color = RemainingColor, IsAntialias = true };
    using var currentPositionPaint = new SKPaint { Color = CurrentPositionColor, IsAntialias = true };

    canvas.DrawRect(boxRect, background);
    canvas.DrawRect(boxRect, border);

    float centerX = boxLeft + BoxSize / 2f;
    float centerY = boxTop + BoxSize / 2f;
    float plotRadius = BoxSize / 2f - Padding;

    if (trajectory == null || trajectory.Length == 0)
      return;

    var projected = new Position[trajectory.Length];
    double maxHorizontalRadius = 0;
    for (int i = 0; i < trajectory.Length; i++)
    {
      projected[i] = ProjectToXY(trajectory[i]);
      double dist = Math.Sqrt(projected[i].X * projected[i].X + projected[i].Y * projected[i].Y);
      if (dist > maxHorizontalRadius)
        maxHorizontalRadius = dist;
    }

    if (maxHorizontalRadius <= 0)
      maxHorizontalRadius = 1;

    float scale = (float)(plotRadius / maxHorizontalRadius);

    float radius = Config.Rs * scale + 3f;
    canvas.DrawCircle(centerX, centerY, radius, centralBodyPaint);

    // Clamp currentIndex into a sensible range. -1 (or any value >= last index)
    // means "everything is completed"; the loop below still splits correctly.
    int splitIndex = ((int)Math.Round(progress)) * (trajectory.Length - 1);

    var screenPoints = new (float X, float Y)[projected.Length];
    for (int i = 0; i < projected.Length; i++)
    {
      screenPoints[i] = (
        centerX + projected[i].X * scale,
        centerY - projected[i].Y * scale // flip Y for standard math orientation
      );
    }

    // Completed segment: index 0..splitIndex.
    using (var completedPath = new SKPathBuilder())
    {
      for (int i = 0; i <= splitIndex; i++)
      {
        var (screenX, screenY) = screenPoints[i];
        if (i == 0)
          completedPath.MoveTo(screenX, screenY);
        else
          completedPath.LineTo(screenX, screenY);

        canvas.DrawCircle(screenX, screenY, PointMarkerRadius, completedPointPaint);
      }

      using var path = completedPath.Detach();
      canvas.DrawPath(path, completedLinePaint);
    }

    // Remaining segment: splitIndex..end. Start the path at splitIndex so the
    // line is continuous with the completed segment rather than showing a gap.
    if (splitIndex < screenPoints.Length - 1)
    {
      using var remainingPath = new SKPathBuilder();
      for (int i = splitIndex; i < screenPoints.Length; i++)
      {
        var (screenX, screenY) = screenPoints[i];
        if (i == splitIndex)
          remainingPath.MoveTo(screenX, screenY);
        else
          remainingPath.LineTo(screenX, screenY);

        if (i > splitIndex)
          canvas.DrawCircle(screenX, screenY, PointMarkerRadius, remainingPointPaint);
      }

      using var path = remainingPath.Detach();
      canvas.DrawPath(path, remainingLinePaint);
    }

    var (currentX, currentY) = InterpolatePosition(projected, (float)progress);
    float screenCurrentX = centerX + currentX * scale;
    float screenCurrentY = centerY - currentY * scale;
    canvas.DrawCircle(screenCurrentX, screenCurrentY, CurrentPositionRadius, currentPositionPaint);

    var paintNorth = new SKPaint { Color = new SKColor(13, 255, 13), IsAntialias = true };
    var paintSouth = new SKPaint { Color = new SKColor(255, 26, 255), IsAntialias = true };
    var paintEast = new SKPaint { Color = new SKColor(255, 13, 13), IsAntialias = true };
    var paintWest = new SKPaint { Color = new SKColor(13, 230, 255), IsAntialias = true };

    // Calcular dimensiones de los rectángulos (25% del ancho)
    float rectWidth = BoxSize * 0.25f;
    float rectThickness = 4f; // Grosor del rectángulo (ajusta según necesites)

    // Norte (arriba, centrado horizontalmente, pegado al borde interior)
    float northLeft = boxLeft + (BoxSize - rectWidth) / 2f;
    float northTop = boxTop;
    canvas.DrawRect(SKRect.Create(northLeft, northTop, rectWidth, rectThickness), paintNorth);

    // Sur (abajo, centrado horizontalmente, pegado al borde interior)
    float southLeft = boxLeft + (BoxSize - rectWidth) / 2f;
    float southTop = boxTop + BoxSize - rectThickness;
    canvas.DrawRect(SKRect.Create(southLeft, southTop, rectWidth, rectThickness), paintSouth);

    // Oeste (izquierda, centrado verticalmente, pegado al borde interior)
    float westLeft = boxLeft;
    float westTop = boxTop + (BoxSize - rectWidth) / 2f;
    canvas.DrawRect(SKRect.Create(westLeft, westTop, rectThickness, rectWidth), paintWest);

    // Este (derecha, centrado verticalmente, pegado al borde interior)
    float eastLeft = boxLeft + BoxSize - rectThickness;
    float eastTop = boxTop + (BoxSize - rectWidth) / 2f;
    canvas.DrawRect(SKRect.Create(eastLeft, eastTop, rectThickness, rectWidth), paintEast);
  }

  private static Position ProjectToXY(CameraKeyframe frame)
  {
    double horizontalRadius = frame.R * Math.Sin(frame.Theta);
    double x = horizontalRadius * Math.Cos(frame.Phi);
    double y = horizontalRadius * Math.Sin(frame.Phi);
    return new Position((float)x, (float)y);
  }

  private static Position InterpolatePosition(Position[] traj, float t)
  {
    if (traj.Length == 1)
      return new Position {
        X = traj[0].X,
        Y = traj[0].Y 
      };

    t = Math.Clamp(t, 0.0f, 1.0f);
    
    float pos = t * (traj.Length - 1);
    int i0 = (int)Math.Floor(pos);
    int i1 = Math.Min(i0 + 1, traj.Length - 1);
    float frac = pos - i0;

    float x = traj[i0].X + (traj[i1].X - traj[i0].X) * frac;
    float y = traj[i0].Y + (traj[i1].Y - traj[i0].Y) * frac;

    return new Position(x, y);
  }
}