using System.IO.Compression;
using System.Text;

namespace BlackHoleRayTracer.Export;

public static class ExportPng
{
  /// <summary>8 bits per channel truecolor PNG, Sub filter per scanline.</summary>
  public static void Export(string path, byte[] rgb, int width, int height)
  {
    using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20);

    fs.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });

    var ihdr = new byte[13];
    WriteBe(ihdr, 0, width);
    WriteBe(ihdr, 4, height);
    ihdr[8] = 8;    // bit depth
    ihdr[9] = 2;    // truecolor RGB
    ihdr[10] = 0;   // deflate compression
    ihdr[11] = 0;   // standard filter method
    ihdr[12] = 0;   // no interlacing
    WriteChunk(fs, "IHDR", ihdr);

    int stride = width * 3;
    var raw = new byte[height * (stride + 1)];
    for (int y = 0; y < height; y++)
    {
      int src = y * stride;
      int dst = y * (stride + 1);
      raw[dst] = 1; // Sub filter: predict from the pixel to the left
      for (int x = 0; x < stride; x++)
      {
        byte left = x >= 3 ? rgb[src + x - 3] : (byte)0;
        raw[dst + 1 + x] = (byte)(rgb[src + x] - left);
      }
    }

    byte[] compressed;
    using (var ms = new MemoryStream())
    {
      using (var z = new ZLibStream(ms, CompressionLevel.Optimal, leaveOpen: true))
        z.Write(raw, 0, raw.Length);
      compressed = ms.ToArray();
    }

    WriteChunk(fs, "IDAT", compressed);
    WriteChunk(fs, "IEND", Array.Empty<byte>());
  }

  private static void WriteChunk(Stream s, string type, byte[] data)
  {
    var len = new byte[4];
    WriteBe(len, 0, data.Length);
    s.Write(len);

    byte[] typeBytes = Encoding.ASCII.GetBytes(type);
    s.Write(typeBytes);
    s.Write(data);

    uint crc = Crc32(data, Crc32(typeBytes, 0xFFFFFFFFu));
    var crcBytes = new byte[4];
    WriteBe(crcBytes, 0, (int)(crc ^ 0xFFFFFFFFu));
    s.Write(crcBytes);
  }

  private static void WriteBe(byte[] buf, int offset, int value)
  {
    buf[offset] = (byte)(value >> 24);
    buf[offset + 1] = (byte)(value >> 16);
    buf[offset + 2] = (byte)(value >> 8);
    buf[offset + 3] = (byte)value;
  }

  private static readonly uint[] CrcTable = BuildCrcTable();

  private static uint[] BuildCrcTable()
  {
    var t = new uint[256];
    for (uint n = 0; n < 256; n++)
    {
      uint c = n;
      for (int k = 0; k < 8; k++)
        c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
      t[n] = c;
    }
    return t;
  }

  /// <summary>
  /// Incremental CRC32. A PNG chunk CRC covers type + data, so the two are chained:
  /// type first, then data, without resetting in between.
  /// </summary>
  private static uint Crc32(byte[] data, uint seed)
  {
    uint c = seed;
    foreach (byte b in data) c = CrcTable[(c ^ b) & 0xFF] ^ (c >> 8);
    return c;
  }
}
