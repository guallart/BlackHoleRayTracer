using BlackHoleRayTracer.Configuration;

using System.IO.Compression;

namespace BlackHoleRayTracer.Export;

/// <summary>
/// Post-processing and image output. No external dependencies: the PNG is written by hand
/// on top of <see cref="ZLibStream"/>.
/// </summary>
public static class ImageWriter
{
  public static void Write(string path, byte[] rgb)
  {
    int width = Config.Camera.Width;
    int height = Config.Camera.Height;
    switch (Config.Output.FrameExtension)
    {
      case ImageExtension.Ppm: ExportPpm.Export(path, rgb, width, height); break;
      case ImageExtension.Png: ExportPng.Export(path, rgb, width, height); break;
    }
  }
}