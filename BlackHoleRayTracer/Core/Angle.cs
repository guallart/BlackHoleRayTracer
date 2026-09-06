using System.Globalization;

namespace BlackHoleRayTracer.Core;

/// <summary>
/// An angle, stored in radians. Degrees exist only at the edges: values authored in
/// Config.cs and text shown to a human.
///
/// There is deliberately no constructor from a bare number — that is the hole a units
/// bug crawls back through. Every angle is built by a factory that names its unit.
///
/// Carries no wrapping semantics, on purpose: a two-turn orbit is phi = 4*pi and must
/// stay that way. Normalising into [0, 2pi) is device-side and lives in GpuMath.
/// </summary>
public readonly struct Angle
{
  public const double DegToRad = Math.PI / 180.0;
  public const double RadToDeg = 180.0 / Math.PI;

  private readonly double _rad;
  private Angle(double rad) => _rad = rad;

  public static Angle FromDegrees(double deg) => new(deg * DegToRad);
  public static Angle FromRadians(double rad) => new(rad);
  public static readonly Angle Zero = new(0.0);

  public double Radians => _rad;
  public double Degrees => _rad * RadToDeg;

  /// <summary>For the kernel-facing structs, which hold raw single-precision radians.</summary>
  public float ToFloatRadians() => (float)_rad;

  public double Cos => Math.Cos(_rad);
  public double Sin => Math.Sin(_rad);

  public static Angle operator +(Angle a, Angle b) => new(a._rad + b._rad);
  public static Angle operator -(Angle a, Angle b) => new(a._rad - b._rad);
  public static Angle operator -(Angle a) => new(-a._rad);
  public static Angle operator *(Angle a, double k) => new(a._rad * k);
  public static Angle operator *(double k, Angle a) => new(a._rad * k);

  /// <summary>Degrees with the unit attached, there is no way to print an angle bare.</summary>
  public override string ToString() => ToString(1);
  public string ToString(int decimals) =>
      Degrees.ToString($"F{decimals}", CultureInfo.InvariantCulture) + " deg";
}
