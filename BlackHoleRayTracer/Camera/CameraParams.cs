using System.Runtime.InteropServices;

namespace BlackHoleRayTracer.Camera;

/// <summary>
/// Camera: static observer at (R, Theta, Phi), looking radially toward the black hole.
/// Pinhole projection.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct CameraParams
{
  public float R;      // radial distance from camera to black hole
  public float Theta;  // camera polar position
  public float Phi;    // camera azimuthal position
  public float FovY;   // vertical field of view (radians)
  public int Width;
  public int Height;

  public readonly int PixelCount => Width * Height;
}