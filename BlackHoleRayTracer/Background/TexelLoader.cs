using System.Globalization;

namespace BlackHoleRayTracer.Background;

/// <summary>Type of background for the scaping rays.</summary>
public enum BackgroundType
{
  /// <summary>Colors by ray status instead of by physics. Diagnostic:
  /// red = absorbed, orange = disk, blue = background, MAGENTA = MaxSteps (a bug).</summary>
  Debug,
  /// <summary>Chess-like pattern with circles in the axis directions.</summary>
  Checker,
  /// <summary>Image of choice.</summary>
  Image,
}

public class TexelLoader
{
  public int Width;
  public int Height;
  public float[] Texels;

  private readonly record struct TexelLoaderResult(float[] Texels, int Width, int Height);

  public TexelLoader()
  {
    TexelLoaderResult result = Configuration.Config.Back.BackType switch
    {
      BackgroundType.Checker => Checker(),
      BackgroundType.Image => LoadRadianceHdr(Configuration.Config.Back.BackImagePath),
      BackgroundType.Debug => default,
      _ => throw new ArgumentException("Invalid value for Config.Back.BackType."),
    };

    Width = result.Width;
    Height = result.Height;
    Texels = result.Texels;
  }

  #region Checkers

  /// <summary>
  /// Analytic debug sky: a checkerboard whose cells have very nearly equal solid angle,
  /// baked into the usual equirectangular store.
  ///
  /// The sphere is cut into <paramref name="bands"/> slices of equal thickness in
  /// cos(theta) (equal area, by Archimedes' hat-box theorem) and each band into a number
  /// of sectors proportional to sin^2(theta), so cells stay about as wide as they are tall
  /// at every latitude. The sector count is forced even so the parity is continuous across
  /// the phi = 0 seam.
  /// </summary>
  /// <param name="supersample">Samples per texel per axis. The pattern has hard edges, so
  /// 1 gives a visibly jagged bake; 3 is plenty.</param>
  private static TexelLoaderResult Checker()
  {
    const int Bands = 16;
    const int SupSample = Configuration.Config.Shading.Supersample;
    const long Width = Configuration.Config.Camera.Width * SupSample;
    const long Height = Configuration.Config.Camera.Height * SupSample;
    const double Dark = 0.03;
    const double Light = 0.85;
    const bool AxisMarkers = true;

    float[] texels = new float[Width * Height * Configuration.Config.ChannelsFull];
    double markerCos = Math.Cos(5.0 * Math.PI / 180.0);
    double invSamples = 1.0 / (SupSample * SupSample);

    Parallel.For(0, Height, y =>
    {
      for (int x = 0; x < Width; x++)
      {
        double accumR = 0.0;
        double accumG = 0.0;
        double accumB = 0.0;

        for (int sy = 0; sy < SupSample; sy++)
        {
          // Sub-texel centres, matching the half-texel convention in SampleUv.
          double theta = (y + (sy + 0.5) / SupSample) / Height * Math.PI;
          double sinTheta = Math.Sin(theta);
          double cosTheta = Math.Cos(theta);

          for (int sx = 0; sx < SupSample; sx++)
          {
            double phi = (x + (sx + 0.5) / SupSample) / Width * 2.0 * Math.PI;
            double xm = sinTheta * Math.Cos(phi);
            double ym = sinTheta * Math.Sin(phi);
            double zm = cosTheta;
            if (AxisMarkers && TryAxisMarker(xm, ym, zm, markerCos, out double r, out double g, out double b))
            {
              accumR += r;
              accumG += g;
              accumB += b;
            }
            else
            {
              double color = CheckerParity(cosTheta, phi, Bands) ? Light : Dark;
              accumR += color;
              accumG += color;
              accumB += color;
            }
          }
        }

        long d = (y * Width + x) * Configuration.Config.ChannelsFull;
        texels[d] = (float)( accumR * invSamples );
        texels[d + 1] = (float)( accumG * invSamples );
        texels[d + 2] = (float)( accumB * invSamples );
        texels[d + 3] = 1.0f;
      }
    });

    return new TexelLoaderResult(
      Texels: texels,
      Width:(int)Width,
      Height:(int)Height
    );
  }

  private static bool CheckerParity(double cosTheta, double phi, int bands)
  {
    int i = (int)((1.0 - cosTheta) * 0.5 * bands);
    if (i < 0) i = 0; else if (i >= bands) i = bands - 1;

    // The band's mid-latitude drives the sector count, so one band shares one grid and the
    // cells do not shear within it.
    double cosMid = 1.0 - (2.0 * i + 1.0) / bands;
    double sinMidSq = Math.Max(1.0 - cosMid * cosMid, 0.0);

    int sectors = (int)Math.Round(Math.PI * bands * sinMidSq);
    if (sectors < 2) sectors = 2;
    if ((sectors & 1) != 0) sectors++;

    int j = (int)(phi / (2.0 * Math.PI) * sectors);
    if (j < 0) j = 0; else if (j >= sectors) j = sectors - 1;

    return ((i + j) & 1) == 0;
  }

  private static readonly (double X, double Y, double Z, double R, double G, double B)[] AxisMarkerTable =
  {
    ( 1.0,  0.0,  0.0, 1.00, 0.05, 0.05),  // +X red
    (-1.0,  0.0,  0.0, 0.05, 0.90, 1.00),  // -X cyan
    ( 0.0,  1.0,  0.0, 0.10, 1.00, 0.10),  // +Y green
    ( 0.0, -1.0,  0.0, 1.00, 0.10, 1.00),  // -Y magenta
    ( 0.0,  0.0,  1.0, 0.15, 0.35, 1.00),  // +Z blue
    ( 0.0,  0.0, -1.0, 1.00, 0.85, 0.10),  // -Z yellow
  };

  private static bool TryAxisMarker(double x, double y, double z, double cosRadius, out double r, out double g, out double b)
  {
    foreach (var m in AxisMarkerTable)
    {
      // Both sides are unit vectors, so the dot product is the cosine of the angle.
      if (m.X * x + m.Y * y + m.Z * z >= cosRadius)
      {
        r = m.R;
        g = m.G;
        b = m.B;
        return true;
      }
    }
    r = g = b = 0.0f;
    return false;
  }

  #endregion


  #region LoadFile

  /// <summary>
  /// Minimal Radiance RGBE reader: header, then either adaptive RLE scanlines (the common
  /// case) or flat/old-RLE ones. Self-contained on purpose, so an HDR sky needs no
  /// third-party image dependency.
  /// </summary>
  private static TexelLoaderResult LoadRadianceHdr(string path)
  {
    string ext = Path.GetExtension(path).ToLowerInvariant();
    if (ext != ".hdr" && ext != ".pic")
      throw new ArgumentException($"Invalid format: only .hdr and .pic are supported. Got '{ext}'");

    using var stream = File.OpenRead(path);
    using var reader = new BinaryReader(stream);

    string magic = ReadAsciiLine(reader);
    if (!magic.StartsWith("#?", StringComparison.Ordinal))
      throw new InvalidDataException("Not a Radiance file.");

    double exposure = 1.0;
    while (true)
    {
      string line = ReadAsciiLine(reader);
      if (line.Length == 0) break;
      if (line.StartsWith("FORMAT=", StringComparison.Ordinal) &&
          !line.Contains("32-bit_rle_rgbe", StringComparison.Ordinal))
        throw new NotSupportedException($"Unsupported Radiance format: {line}");
      if (line.StartsWith("EXPOSURE=", StringComparison.Ordinal) &&
          double.TryParse(line.AsSpan(9), NumberStyles.Float, CultureInfo.InvariantCulture, out double e))
        exposure *= e;
    }

    string[] res = ReadAsciiLine(reader).Split(' ', StringSplitOptions.RemoveEmptyEntries);
    if (res.Length != 4 || res[0] != "-Y" || res[2] != "+X")
      throw new NotSupportedException("Only -Y <h> +X <w> scanline ordering is supported.");

    int height = int.Parse(res[1], CultureInfo.InvariantCulture);
    int width = int.Parse(res[3], CultureInfo.InvariantCulture);

    var texels = new double[(long)width * height * Configuration.Config.ChannelsFull];
    var scan = new byte[width * 4];
    // EXPOSURE is the factor already applied to the stored values, so divide it out.
    double scale = 1.0 / (exposure == 0.0 ? 1.0 : exposure);

    for (int y = 0; y < height; y++)
    {
      ReadScanline(reader, scan, width);
      RgbeToLinear(scan, texels, y * width * Configuration.Config.ChannelsFull, width, scale);
    }

    return new TexelLoaderResult(
      Texels: texels.Select(t => (float)t).ToArray(),
      Width: width,
      Height: height
    );
  }

  private static string ReadAsciiLine(BinaryReader reader)
  {
    var sb = new System.Text.StringBuilder(64);
    while (true)
    {
      byte c = reader.ReadByte();
      if (c == (byte)'\n') break;
      if (c != (byte)'\r') sb.Append((char)c);
    }
    return sb.ToString();
  }

  private static void ReadScanline(BinaryReader reader, byte[] scan, int width)
  {
    if (width < 8 || width > 0x7fff) { ReadFlatScanline(reader, scan, width, 0); return; }

    byte b0 = reader.ReadByte(), b1 = reader.ReadByte(), b2 = reader.ReadByte(), b3 = reader.ReadByte();
    if (b0 != 2 || b1 != 2 || ((b2 << 8) | b3) != width)
    {
      // Not an adaptive-RLE header: those four bytes were the first pixel.
      scan[0] = b0; scan[1] = b1; scan[2] = b2; scan[3] = b3;
      ReadFlatScanline(reader, scan, width, 1);
      return;
    }

    // Adaptive RLE stores the four channels as separate runs.
    for (int c = 0; c < 4; c++)
    {
      int x = 0;
      while (x < width)
      {
        int count = reader.ReadByte();
        if (count > 128)
        {
          count -= 128;
          if (x + count > width) throw new InvalidDataException("Corrupt RLE run.");
          byte value = reader.ReadByte();
          while (count-- > 0) scan[x++ * 4 + c] = value;
        }
        else
        {
          if (count == 0 || x + count > width) throw new InvalidDataException("Corrupt RLE run.");
          while (count-- > 0) scan[x++ * 4 + c] = reader.ReadByte();
        }
      }
    }
  }

  private static void ReadFlatScanline(BinaryReader reader, byte[] scan, int width, int start)
  {
    int shift = 0;
    int x = start;
    while (x < width)
    {
      byte r = reader.ReadByte(), g = reader.ReadByte(), b = reader.ReadByte(), e = reader.ReadByte();
      if (r == 1 && g == 1 && b == 1 && x > 0)
      {
        // Old-style RLE: repeat the previous pixel, count possibly spread over consecutive markers.
        int count = e << shift;
        shift += 8;
        int prev = (x - 1) * 4;
        while (count-- > 0 && x < width) { Array.Copy(scan, prev, scan, x * 4, 4); x++; }
      }
      else
      {
        int p = x * 4;
        scan[p] = r; scan[p + 1] = g; scan[p + 2] = b; scan[p + 3] = e;
        x++;
        shift = 0;
      }
    }
  }

  private static void RgbeToLinear(byte[] scan, double[] dst, int offset, int width, double scale)
  {
    for (int x = 0; x < width; x++)
    {
      int s = x * 4;
      int d = offset + x * Configuration.Config.ChannelsFull;
      int e = scan[s + 3];

      if (e == 0)
      {
        dst[d] = dst[d + 1] = dst[d + 2] = 0.0f;
      }
      else
      {
        // RGBE mantissas are in [0,255) with an implicit 1/256, hence the -(128+8).
        double f = (double)Math.ScaleB(1.0, e - 136) * scale;
        dst[d] = scan[s] * f;
        dst[d + 1] = scan[s + 1] * f;
        dst[d + 2] = scan[s + 2] * f;
      }
      dst[d + 3] = 1.0f;
    }
  }

  #endregion
}
