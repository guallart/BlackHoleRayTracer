using System.Text;

namespace BlackHoleRayTracer.Diagnostics;

public sealed class TeeTextWriter : TextWriter
{
  private readonly TextWriter[] _writers;
  private readonly object _lock = new();

  public TeeTextWriter(params TextWriter[] writers) => _writers = writers;

  public override Encoding Encoding => _writers[0].Encoding;

  public override void Write(char value)
  {
    lock (_lock)
      foreach (var w in _writers) w.Write(value);
  }

  public override void Write(string? value)
  {
    lock (_lock)
      foreach (var w in _writers) w.Write(value);
  }

  public override void WriteLine(string? value)
  {
    lock (_lock)
      foreach (var w in _writers) w.WriteLine(value);
  }

  public override void Flush()
  {
    lock (_lock)
      foreach (var w in _writers) w.Flush();
  }
}
