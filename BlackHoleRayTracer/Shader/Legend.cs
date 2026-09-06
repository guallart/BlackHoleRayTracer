namespace BlackHoleRayTracer.Shader;

using BlackHoleRayTracer.Camera;
using BlackHoleRayTracer.Configuration;
using BlackHoleRayTracer.Rays;

using SkiaSharp;

using System.Runtime.InteropServices;

public static class Legend
{
  private const int Separation = 8;
  private static string[] GetLines(TraceStats stats, CameraParams cam)
  {
    return new[]
      {
        $"r             {cam.R,Separation:F1} RS",
        $"theta         {cam.Theta,Separation:F1} deg",
        $"phi           {cam.Phi * 180 / Math.PI,Separation:F1} deg",
        $"absorbed rays {(float)stats.Absorbed / stats.Total * 100,Separation:F1} %",
        $"hit disk rays {(float)stats.HitDisk / stats.Total * 100,Separation:F1} %",
        $"escaped rays  {(float)stats.Escaped / stats.Total * 100,Separation:F1} %",
        $"steps/ray     {stats.AverageSteps,Separation:F1}",
        $"max steps     {stats.MaxStepsSingleRay,Separation}"
      };
  }

  public static void Draw(byte[] rgb, TraceStats stats, CameraParams cam)
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
      SKTypeface fontFamily = SKTypeface.FromFamilyName("DejaVu Sans Mono") ?? SKTypeface.Default;
      using var font = new SKFont(fontFamily, 15);
      using var box = new SKPaint { Color = new SKColor(0, 0, 0, 240) };
      using var text = new SKPaint { Color = SKColors.White, IsAntialias = true };

      const float pad = 12f, margin = 20f;
      float lineH = font.Spacing;
      float boxW = 0;

      string[] lines = GetLines(stats, cam);
      foreach (string s in lines) 
        boxW = Math.Max(boxW, font.MeasureText(s));

      boxW += pad * 2;
      float boxH = lines.Length * lineH + pad * 2;

      canvas.DrawRect(SKRect.Create(margin, margin, boxW, boxH), box);

      float y = margin + pad - font.Metrics.Ascent;
      foreach (string s in lines)
      {
        canvas.DrawText(s, margin + pad, y, SKTextAlign.Left, font, text);
        y += lineH;
      }
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
}
