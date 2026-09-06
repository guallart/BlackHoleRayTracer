using BlackHoleRayTracer.Core;

using System.Runtime.InteropServices;

namespace BlackHoleRayTracer.Rays;

/// <summary>
/// State of a null geodesic, expressed in the ray's own orbital plane.
///
/// The state alone (R, Phi, Pr, E, L, Lambda, Steps, Status) is enough to integrate,
/// but not enough to reconstruct anything in 3D: rotating each ray into its own
/// equatorial plane means Phi is an angle in a different plane for every pixel.
/// Without knowing that plane, it's impossible to detect a disk crossing (which lives
/// in the GLOBAL equator, not the ray's) or to map an escaped ray to a skybox direction.
///
/// To fix this, the struct also carries the orthonormal basis of the orbital plane
/// (PlaneE1, PlaneE2), set once at ray generation and constant through integration:
///
///     position(lambda) = R * ( cos(Phi)*PlaneE1 + sin(Phi)*PlaneE2 )
///
/// PlaneE1 is the camera's initial radial direction (so Phi starts at 0), and
/// PlaneE2 is the photon's initial tangential direction (so L >= 0 always).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct RayState
{
  public float R;
  public float Phi;
  public float Pr;      // dr/dLambda
  public float E;       // conserved energy
  public float L;       // conserved angular momentum (>= 0 by construction)
  public float Lambda;  // accumulated affine parameter (diagnostic)

  public Vec3 PlaneE1;   // orbital plane basis: initial radial direction
  public Vec3 PlaneE2;   // orbital plane basis: initial tangential direction

  public int Steps;      // integration steps consumed
  public RayStatus Status;

  /// <summary>Global Cartesian position for the current state.</summary>
  public readonly Vec3 Position()
  {
    float c = MathF.Cos(Phi);
    float s = MathF.Sin(Phi);
    return R * (c * PlaneE1 + s * PlaneE2);
  }

  /// <summary>
  /// Coordinate velocity dx/dLambda in global Cartesian coordinates:
  /// v = Pr*e_r + (L/R)*e_phi, with e_r and e_phi rotated by Phi within the plane.
  /// </summary>
  public readonly Vec3 Velocity()
  {
    float c = MathF.Cos(Phi);
    float s = MathF.Sin(Phi);
    Vec3 er = c * PlaneE1 + s * PlaneE2;
    Vec3 ephi = -s * PlaneE1 + c * PlaneE2;
    return Pr * er + (R != 0.0f ? L / R : 0.0f) * ephi;
  }
}