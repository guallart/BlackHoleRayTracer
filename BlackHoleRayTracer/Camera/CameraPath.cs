using BlackHoleRayTracer.Configuration;
using BlackHoleRayTracer.Core;

namespace BlackHoleRayTracer.Camera;

/// <summary>Interpolated camera path.</summary>
public sealed class CameraPath
{
  public CameraKeyframe[] Keys { get; }

  public CameraPath()
  {
    Keys = Config.Mode switch
    {

      RunMode.Image => SingleImageFrames(),
      RunMode.CustomPath => Config.Video.CustomKeyframes,
      RunMode.FreeFall => TrajectoryIntegrator.CamerPathFromTrajectory(),
      _ => throw new ArgumentException("Run mode not valid.")
    };
  }
  
  public static CameraKeyframe[] SingleImageFrames()
  {
    double r = Config.Camera.R;
    double t = 0.0;
    Angle theta = Config.Camera.Theta;
    Angle phi = Config.Camera.Phi;
    Angle fov = Config.Camera.Fov;
    return [new CameraKeyframe(r, t, theta, phi, fov)];
  }

  public float Duration => (float)( Keys[^1].Time - Keys[0].Time );

  public CameraParams Sample(double time)
  {
    Interpolate(time, out double r, out double theta, out double phi, out double fov);
    return new CameraParams
    {
      R = (float)r * Config.Rs,
      Theta = (float)theta,
      Phi = (float)phi,
      FovY = (float)fov,
      Width = Config.Camera.Width * Config.Shading.Supersample,
      Height = Config.Camera.Height * Config.Shading.Supersample
    };
  }

  private void Interpolate(double t, out double r, out double theta, out double phi, out double fov)
  {
    if (Keys.Length == 1 || t <= Keys[0].Time)
    {
      var k0 = Keys[0];
      r = k0.R; theta = k0.Theta; phi = k0.Phi; fov = k0.Fov;
      return;
    }

    if (t >= Keys[^1].Time)
    {
      var kn = Keys[^1];
      r = kn.R; theta = kn.Theta; phi = kn.Phi; fov = kn.Fov;
      return;
    }

    int i = 0;
    while (i < Keys.Length - 2 && Keys[i + 1].Time < t) i++;

    CameraKeyframe a = Keys[i], b = Keys[i + 1];
    CameraKeyframe pre = Keys[Math.Max(i - 1, 0)];
    CameraKeyframe post = Keys[Math.Min(i + 2, Keys.Length - 1)];

    double span = b.Time - a.Time;
    double u = span > 0.0 ? (t - a.Time) / span : 0.0;

    // Catmull-Rom: velocity continuity across keyframes. Over a linear ramp (the case of
    // a constant-speed orbit) it reproduces the straight line exactly, so it introduces
    // no spurious wobble in azimuth.
    r = CatmullRom(pre.R, a.R, b.R, post.R, u);
    theta = CatmullRom(pre.Theta, a.Theta, b.Theta, post.Theta, u);
    phi = CatmullRom(pre.Phi, a.Phi, b.Phi, post.Phi, u);
    fov = CatmullRom(pre.Fov, a.Fov, b.Fov, post.Fov, u);
  }

  private static double CatmullRom(double p0, double p1, double p2, double p3, double t)
  {
    double t2 = t * t;
    double t3 = t2 * t;
    return 0.5 * ((2.0 * p1)
                + (-p0 + p2) * t
                + (2.0 * p0 - 5.0 * p1 + 4.0 * p2 - p3) * t2
                + (-p0 + 3.0 * p1 - 3.0 * p2 + p3) * t3);
  }
}
