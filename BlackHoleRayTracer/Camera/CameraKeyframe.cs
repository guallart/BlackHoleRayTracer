using BlackHoleRayTracer.Core;

namespace BlackHoleRayTracer.Camera;

/// <summary>A control point on the camera path. Time is expressed in geometrized units.</summary>
public readonly struct CameraKeyframe(double r, double t, Angle theta, Angle phi, Angle fov)
{
  public double R { get; } = r;
  public double Time { get; } = t;
  public double Theta { get; } = theta.Radians;
  public double Phi { get; } = phi.Radians;
  public double Fov { get; } = fov.Radians;
}
