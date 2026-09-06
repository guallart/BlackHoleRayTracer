using System.Runtime.InteropServices;

namespace BlackHoleRayTracer.Rays;

/// <summary>
/// Final per-pixel output. The only thing consumed by the downstream shading stage.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct RayResult
{
  public int PixelX;
  public int PixelY;
  public RayStatus Status;

  // Valid if Status == Escaped (or MaxStepsReached, as a best estimate):
  // final propagation direction, for sampling the skybox.
  public float EscapeTheta;
  public float EscapePhi;

  // Valid if Status == HitDisk: intersection point on the GLOBAL equator.
  public float DiskR;
  public float DiskPhi;

  // GLOBAL z-component of the photon's angular momentum, divided by its energy:
  // b_z = L_z/E. Invariant under z-rotation (so one trace can be reused across a
  // full camera orbit), and along with DiskR is all that's needed for the disk's
  // redshift factor g. Without this field, Doppler shift can't be computed: the L
  // in RayState is the magnitude in the ray's own plane, not its z-projection.
  public float BzGlobal;

  // Debug metadata and hooks for future effects (Doppler, redshift, beaming).
  public float FinalPr;
  public int StepsTaken;
}
