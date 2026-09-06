using BlackHoleRayTracer.Background;
using BlackHoleRayTracer.Camera;
using BlackHoleRayTracer.Core;

namespace BlackHoleRayTracer.Configuration;

/// <summary>
/// Units: geometrized (G = c = 1). Distances are expressed as multiples of <see cref="Rs"/>,
/// so with Rs = 1 the numbers are directly Schwarzschild radii.
/// Configuration angles are in DEGREES (conversion to radians is internal);
/// times are in geometrized units, not video seconds.
/// </summary>
public static class Config
{
  #region DontTouchParameters
  /// <summary>
  /// Schwarzschild radius. The scale of the problem; keeping it at 1 is convenient.
  /// Some parts of the code have been simplified taking into account that Rs is exactly 1,
  /// so changing might break the code.
  /// </summary>
  public const float Rs = 1.0f;
  /// <summary>Number of chanels in an RGBA value.</summary>
  public const int ChannelsFull = 4;
  /// <summary>Number of chanels in an RGB value.</summary>
  public const int ChannelsReduced = 3;
  #endregion

  /// <summary>Image (single video) or Video.</summary>
  public const RunMode Mode = RunMode.Image;

  public static class Camera
  {
    /// <summary>Distance to the black hole, in rs. Must be > 1.</summary>
    public const float R = 10.0f;
    /// <summary>
    /// Colatitude. 90 deg = edge-on to the disk (maximum Doppler, disk seen as a line);
    /// 0 deg = viewed from the pole. Between 80 deg and 85 deg is where the far-side arc
    /// curving above the shadow becomes visible.
    /// </summary>
    public readonly static Angle Theta = Angle.FromDegrees(82.0);
    /// <summary>Azimuth. Irrelevant for a still image: the system is axisymmetric.</summary>
    public readonly static Angle Phi = Angle.FromDegrees(45.0);
    /// <summary>Vertical field of view, in degrees.</summary>
    public readonly static Angle Fov = Angle.FromDegrees(28.6 * 2);
    public const int Width = 1280;
    public const int Height = 720;
  }

  public static class Solver
  {
    /// <summary>Cutoff radius for "infinity", in rs. Increasing it improves the
    /// asymptotic direction used for the star background, at low cost (step size
    /// grows with r).</summary>
    public const float REscape = 2_000.0f;
    public const int MaxSteps = 5_000;
    /// <summary>Relative margin over rs before a ray is considered absorbed.</summary>
    public const float HorizonEpsilon = 1e-4f;
    public const bool UseAdaptiveStep = true;
    /// <summary>Safety factor for the adaptive step. Lower = more accurate and
    /// slower; the integrator error drops by 16x when halved (RK4 is fourth order).</summary>
    public const float AdaptiveSafety = 0.1f;
    /// <summary>Δphi bound per step, in radians.</summary>
    public const float MaxAnglePerStep = 0.05f;
    /// <summary>Fixed step, used only if UseAdaptiveStep = false.</summary>
    public const float FixedLambdaStep = 0.05f;
    /// <summary>Minimum lambda step used if UseAdaptiveStep = true.</summary>
    public const float MinLambdaStep = 1e-4f;
    /// <summary>Maximum lambda step used if UseAdaptiveStep = true.</summary>
    public const float MaxLambdaStep = 100.0f;
  }

  public static class Shading
  {
    /// <summary>
    /// NxN supersampling. Traces N^2 times more rays and averages them. This is the
    /// dominant cost: going from 2 to 3 multiplies the time by 2.25. With 1, the
    /// shadow edge and the photon ring appear visibly jagged.
    /// </summary>
    public const int Supersample = 2;
    /// <summary>Linear gain before tone mapping.</summary>
    public const float Exposure = 0.45f;
    /// <summary>Multiplier for the rgb values that scaped and reached the background.</summary>
    public const float SkyBrightness = 1.0f;
  }

  public static class Disk
  {
    /// <summary>Enables the accretion disk.</summary>
    public const bool DiskEnabled = false;
    /// <summary>Inner edge of the disk, in rs. The ISCO is at 6M = 3*rs.</summary>
    public const float DiskInner = 3.0f;
    /// <summary>Outer edge of the disk, in rs.</summary>
    public const float DiskOuter = 14.0f;
    /// <summary>Disk temperature at the peak of its radial profile, in kelvin.
    /// ~3000 K gives a red-orange disk; ~8000 K warm white; >15000 K bluish.</summary>
    public const float PeakTemperature = 3000.0f;
    /// <summary>Relativistic disk physics. False forces g = 1: disables Doppler and
    /// redshift, useful to see at a glance how much they contribute.</summary>
    public const bool Relativistic = true;
    /// <summary>Intensity of the co-rotating disk mottling. 0 = smooth axisymmetric disk.</summary>
    public const float TextureStrength = 0.38f;
    public const float TextureScaleRadial = 5.0f;
    public const float TextureScaleAngular = 8.0f;
    /// <summary>Simulated instant, for Image mode. Changes the phase of the disk material.
    /// In Video mode this is the initial time of the disk.</summary>
    public const float InitialTime = 0.0f;
  }

  public static class Back
  {
    /// <summary>Background to represent the scaped rays.</summary>
    public const BackgroundType BackType = BackgroundType.Checker;
    /// <summary>The path for the image background must be provided if BackType == BackgroundType.Image</summary>
    public const string? BackImagePath = @"C:\dev\BlackHoleRayTracer\Assets\convertio.in_starmap_2020_4k_print-4109719988.hdr";
  }

  public static class Output
  {
    /// <summary>Paths relative to the solution folder.</summary>
    public const string FileName = "blackhole";
    public const string FramesDirectory = "Frames";
    public const string OutputDirectory = "Outputs";
    public const ImageExtension FrameExtension = ImageExtension.Png;
  }

  public static class Video
  {
    public const int Frames = Fps * 60;
    public const int Fps = 24;
    /// <summary>
    /// Coordinate time simulated across the video, in geometrized units.
    /// NaN = automatic: 2 orbits of the brightest ring of the disk. 
    /// </summary>
    public const double Duration = double.NaN;

    /// <summary>
    /// Custom trajectory, used if Path = Custom. Each entry is
    /// [time, radius (in rs), theta (deg), phi (deg), fov (deg)]
    /// phi is not wrapped: 720 means two full turns.
    /// </summary>
    public static readonly CameraKeyframe[] CustomKeyframes =
    {
        new(t:   0.0, r: 45.0, theta: Angle.FromDegrees(89.0), phi: Angle.FromDegrees(  0.0), fov: Angle.FromDegrees(28.6)),
        new(t:  60.0, r: 30.0, theta: Angle.FromDegrees(85.0), phi: Angle.FromDegrees( 90.0), fov: Angle.FromDegrees(28.6)),
        new(t: 120.0, r: 16.0, theta: Angle.FromDegrees(70.0), phi: Angle.FromDegrees(210.0), fov: Angle.FromDegrees(28.6)),
        new(t: 150.0, r: 14.0, theta: Angle.FromDegrees(62.0), phi: Angle.FromDegrees(270.0), fov: Angle.FromDegrees(32.0))
    };

    /// <summary>Initial values for the freefall trajectory.</summary>
    public const double X0 = 50.0;
    public const double Y0 = 75.0;
    public const double Z0 = 10.0;
    public const double VelX0 = -5.0;
    public const double VelY0 = -7.5;
    public const double VelZ0 = -1.0;
  }
}