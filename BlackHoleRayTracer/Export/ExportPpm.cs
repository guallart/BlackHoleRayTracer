using System.Text;

namespace BlackHoleRayTracer.Export;

public static class ExportPpm
{
  /// <summary>
  /// Binary PPM (P6). Uncompressed and wasteful, but ffmpeg reads it directly and writing
  /// it costs almost nothing: over long sequences it saves more CPU time in compression
  /// than it spends on disk.
  /// </summary>
  public static void Export(string path, byte[] rgb, int width, int height)
  {
    using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20);
    fs.Write(Encoding.ASCII.GetBytes($"P6\n{width} {height}\n255\n"));
    fs.Write(rgb);
  }
}

