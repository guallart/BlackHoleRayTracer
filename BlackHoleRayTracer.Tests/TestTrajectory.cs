using BlackHoleRayTracer.Camera;
using BlackHoleRayTracer.Configuration;
using BlackHoleRayTracer.Core;
using BlackHoleRayTracer.Shader;

using SkiaSharp;

using System.Runtime.InteropServices;

namespace BlackHoleRayTracer.Tests;

/// <summary>
/// Not a correctness test in the assertion sense, there's no reference image to
/// compare against. This produces a rendered trajectory plot as a PNG so it can be
/// visually inspected after the run.
/// </summary>
[TestClass]
public class TrajectoryPlotTests
{
  public TestContext TestContext { get; set; } = null!;

  [TestMethod]
  public void Draw_RendersOrbit_ProducesPngForInspection()
  {
    int width = Config.Camera.Width;
    int height = Config.Camera.Height;

    // Plain black background so the plot box stands out.
    var rgb = new byte[width * height * 3];

    CameraKeyframe[] trajectory = TrajectoryIntegrator.CamerPathFromTrajectory();

    TrajectoryPlot.Draw(rgb, trajectory, 0.5);

    string outputDir = Paths.OutputDirectory;
    string outputPath = SaveAsPng(rgb, width, height, outputDir, "trajectory_plot_orbit.png");

    TestContext.WriteLine($"Trajectory plot written to: {outputPath}");

    Assert.IsTrue(File.Exists(outputPath), "Expected the rendered trajectory plot PNG to be written to disk.");
  }

  private static string SaveAsPng(byte[] rgb, int width, int height, string outputDir, string fileName)
  {
    var rgba = new byte[width * height * 4];
    for (int i = 0, j = 0; i < rgb.Length; i += 3, j += 4)
    {
      rgba[j] = rgb[i];
      rgba[j + 1] = rgb[i + 1];
      rgba[j + 2] = rgb[i + 2];
      rgba[j + 3] = 255;
    }

    var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque);
    using var bmp = new SKBitmap(info);
    Marshal.Copy(rgba, 0, bmp.GetPixels(), rgba.Length);

    using var image = SKImage.FromBitmap(bmp);
    using var data = image.Encode(SKEncodedImageFormat.Png, 100);

    Directory.CreateDirectory(outputDir);
    string outputPath = Path.Combine(outputDir, fileName);

    using var stream = File.OpenWrite(outputPath);
    data.SaveTo(stream);

    return outputPath;
  }
}