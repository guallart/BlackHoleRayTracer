using BlackHoleRayTracer.Camera;
using BlackHoleRayTracer.Core;

using ILGPU;

namespace BlackHoleRayTracer.Rays;

/// <summary>
/// Converts each pixel (i, j) into a valid initial <see cref="RayState"/>.
/// </summary>
public static class CameraRayGenerator
{
  /// <summary>Below this threshold the ray is treated as purely radial (L = 0).</summary>
  private const double TangentialEpsilon = 1e-14;

  /// <summary>
  /// Generates the array of initial rays, row-major layout: index = y * Width + x.
  /// Requires rs (from SimParams) because the redshift factor f = 1 - rs/R enters
  /// the normalization of E and p_r.
  /// </summary>
  public static void Generate(Index1D idx, CameraParams cam, ArrayView<RayState> rays)
  {
    int x = idx % cam.Width;
    int y = idx / cam.Width;
    float rs = Configuration.Config.Rs;

    // Orthonormal tetrad of the static observer, expressed in global Cartesian coordinates.
    // The spatial legs of the tetrad are e_r = sqrt(f) d_r, e_theta = (1/r) d_theta,
    // e_phi = (1/(r sinTheta)) d_phi. They're already normalized, so their Cartesian
    // representation is just the usual spherical unit vectors: this lets us treat the
    // local direction as an ordinary Cartesian unit vector.
    float st = MathF.Sin(cam.Theta), ct = MathF.Cos(cam.Theta);
    float sp = MathF.Sin(cam.Phi), cp = MathF.Cos(cam.Phi);

    var eR = new Vec3(st * cp, st * sp, ct);
    var eTheta = new Vec3(ct * cp, ct * sp, -st);
    var ePhi = new Vec3(-sp, cp, 0.0f);

    // Camera basis: looks toward the black hole (-e_r), "up" is -e_theta (theta
    // decreases toward the north pole). The right axis turns out to be e_phi, so
    // increasing x corresponds to increasing phi.
    Vec3 forward = -1.0f * eR;
    Vec3 up = -1.0f * eTheta;
    Vec3 right = ePhi;

    float tanHalfFov = MathF.Tan(0.5f * cam.FovY);
    float aspect = (float)cam.Width / cam.Height;

    float f = 1.0f - rs / cam.R;
    float sqrtF = MathF.Sqrt(f);

    float ndcY = (1.0f - 2.0f * (y + 0.5f) / cam.Height) * tanHalfFov;
    float ndcX = (2.0f * (x + 0.5f) / cam.Width - 1.0f) * aspect * tanHalfFov;

    Vec3 dir = (forward + ndcX * right + ndcY * up).Normalized();

    rays[idx] = FromLocalDirection(dir, eR, cam, sqrtF);
  }

  /// <summary>
  /// Builds the RayState from the photon's local unit direction n.
  ///
  /// With E_local = 1 (the affine parameter scale is free) and f = 1 - rs/R:
  ///     E   = sqrt(f)                 (conserved energy, = f * dt/dLambda)
  ///     L   = R * n_t                 (angular momentum in the ray's own plane)
  ///     p_r = n_r * sqrt(f)           (dr/dLambda)
  /// where n_r = n . e_r and n_t = |n - n_r * e_r|.
  ///
  /// Null check: p_r^2 = E^2 - f*L^2/R^2  <=>  n_r^2 * f = f - f*n_t^2  <=>  n_r^2 + n_t^2 = 1.
  /// The impact parameter is b = L/E = R*n_t/sqrt(f), with n_t = sin(alpha):
  /// exactly the standard relation sin(alpha) = b*sqrt(f)/R for a static observer.
  /// </summary>
  private static RayState FromLocalDirection(in Vec3 dir, in Vec3 eR, in CameraParams cam, float sqrtF)
  {
    float nr = Vec3.Dot(dir, eR);
    Vec3 tangential = dir - nr * eR;
    float nt = tangential.Length;

    Vec3 e1 = eR; // Phi = 0 at the camera's position
    Vec3 e2;

    if (nt > TangentialEpsilon)
    {
      e2 = tangential * (1.0f / nt);
    }
    else
    {
      // Purely radial ray: the orbital plane is degenerate.
      // Any perpendicular vector works, since L = 0 and Phi will never change.
      nt = 0.0f;
      e2 = AnyPerpendicular(e1);
    }

    return new RayState
    {
      R = cam.R,
      Phi = 0.0f,
      Pr = nr * sqrtF,
      E = sqrtF,
      L = cam.R * nt,
      Lambda = 0.0f,
      PlaneE1 = e1,
      PlaneE2 = e2,
      Steps = 0,
      Status = RayStatus.Active
    };
  }

  private static Vec3 AnyPerpendicular(in Vec3 v)
  {
    Vec3 axis = MathF.Abs(v.X) < 0.9f
      ? new Vec3(1.0f, 0.0f, 0.0f) 
      : new Vec3(0.0f, 1.0f, 0.0f);

    return Vec3.Cross(v, axis).Normalized();
  }
}