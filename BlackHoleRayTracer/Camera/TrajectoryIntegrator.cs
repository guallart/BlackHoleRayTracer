using BlackHoleRayTracer.Configuration;
using BlackHoleRayTracer.Core;

namespace BlackHoleRayTracer.Camera;

public static class TrajectoryIntegrator
{
  private const double GM = 1.0 * Config.Rs / 2.0;
  private const int MAX_ITERS = 1000000;
  private const double H = 0.1;

  private struct State
  {
    public double r, theta, phi;
    public double dr, dtheta, dphi;

    public State(double r, double theta, double phi, double dr, double dtheta, double dphi)
    {
      this.r = r;
      this.theta = theta;
      this.phi = phi;
      this.dr = dr;
      this.dtheta = dtheta;
      this.dphi = dphi;
    }

    public static State operator +(State a, State b) => new(a.r + b.r, a.theta + b.theta, a.phi + b.phi, a.dr + b.dr, a.dtheta + b.dtheta, a.dphi + b.dphi);
    public static State operator *(double s, State a) => new(s*a.r, s * a.theta, s * a.phi, s * a.dr, s * a.dtheta, s * a.dphi);
  }

  public static CameraKeyframe[] CamerPathFromTrajectory()
  {
    double x0 = Config.Video.X0;
    double y0 = Config.Video.Y0;
    double z0 = Config.Video.Z0;
    double vx0 = Config.Video.VelX0;
    double vy0 = Config.Video.VelY0;
    double vz0 = Config.Video.VelZ0;

    (double r0, double theta0, double phi0) = PositionToSpherical(x0, y0, z0);
    (double dr0, double dtheta0, double dphi0) = VelocityToSpherical(x0, y0, z0, vx0, vy0, vz0);
    State s0 = new State(r0,  theta0, phi0, dr0, dtheta0, dphi0);

    State[] points = ComputeTrajectory(s0);
    Angle fov = Config.Camera.Fov;
    var frames = new List<CameraKeyframe>();

    for (int i = 0; i < points.Length; i++)
    {
      double t = i * H;
      State s = points[i];
      Angle theta = Angle.FromRadians(s.theta);
      Angle phi = Angle.FromRadians(s.phi);
      frames.Add(new CameraKeyframe(s.r, t, theta, phi, fov));
    }

    return frames.ToArray();
  }

  private static (double r, double theta, double phi) PositionToSpherical(double x, double y, double z)
  {
    double r = Math.Sqrt(x*x +  y*y + z*z);
    double theta = Math.Acos(z / r);
    double phi = Math.Atan2(y, x);
    return (r, theta, phi);
  }

  private static (double dr, double dtheta, double dphi) VelocityToSpherical(double x, double y, double z, double vx, double vy, double vz)
  {
    double r = Math.Sqrt(x * x + y * y + z * z);
    double theta = Math.Acos(z / r);
    double phi = Math.Atan2(y, x);

    double sinT = Math.Sin(theta), cosT = Math.Cos(theta);
    double sinP = Math.Sin(phi), cosP = Math.Cos(phi);

    // r_hat, theta_hat, phi_hat
    double dr = vx * sinT * cosP + vy * sinT * sinP + vz * cosT;
    double dtheta = (vx * cosT * cosP + vy * cosT * sinP - vz * sinT) / r;
    double dphi = (-vx * sinP + vy * cosP) / (r * sinT);

    return (dr, dtheta, dphi);
  }

  private static State[] ComputeTrajectory(State s)
  {
    var states = new List<State>();
    states.Add(s);

    for (int i = 0; i < MAX_ITERS; i++)
    {
      s = Rk4Step(s, H);

      if (s.r <= 1.0)
        break;

      states.Add(s);
    }

    return states.ToArray();
  }

  private static State Rk4Step(State s, double h)
  {
    Rhs(               s, out State k1);
    Rhs(s + 0.5 * h * k1, out State k2);
    Rhs(s + 0.5 * h * k2, out State k3);
    Rhs(      s + h * k3, out State k4);

    return s + h / 6.0 * (k1 + 2.0 * k2 + 2.0 * k3 + k4);
  }

  private static void Rhs(State s, out State ds)
  {
    ds.r = s.dr;
    ds.theta = s.dtheta;
    ds.phi = s.dphi;

    double sinTheta = Math.Sin(s.theta);
    double cosTheha = Math.Cos(s.theta);
    double cotTheta = 1.0 / Math.Tan(s.theta);
    ds.dr = s.r * s.dtheta * s.dtheta + s.r * sinTheta * sinTheta * s.dphi * s.dphi - GM / (s.r * s.r);
    ds.dtheta = -2.0 * s.dr * s.dtheta / s.r + sinTheta * cosTheha * s.dphi * s.dphi;
    ds.dphi = -2.0 * s.dr * s.dphi / s.r - 2.0 * cotTheta * s.dtheta * s.dphi;
  }
}
