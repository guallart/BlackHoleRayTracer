using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace BlackHoleRayTracer.Core;

/// <summary>
/// 3D vector in global Cartesian coordinates. Blittable struct, no allocations.
/// Generic over the floating-point precision (float or double).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct Vec3
{
  public readonly float X;
  public readonly float Y;
  public readonly float Z;

  public Vec3(float x, float y, float z) { X = x; Y = y; Z = z; }

  public static Vec3 operator +(Vec3 a, Vec3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
  public static Vec3 operator -(Vec3 a, Vec3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
  public static Vec3 operator *(Vec3 a, float s) => new(a.X * s, a.Y * s, a.Z * s);
  public static Vec3 operator *(float s, Vec3 a) => new(a.X * s, a.Y * s, a.Z * s);

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static float Dot(in Vec3 a, in Vec3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

  public static Vec3 Cross(in Vec3 a, in Vec3 b) => new(
      a.Y * b.Z - a.Z * b.Y,
      a.Z * b.X - a.X * b.Z,
      a.X * b.Y - a.Y * b.X);

  public float Length => float.Sqrt(X * X + Y * Y + Z * Z);

  public Vec3 Normalized()
  {
    float len = Length;
    return len > 0.0f ? this * (1.0f / len) : new Vec3(0.0f, 0.0f, 0.0f);
  }
}