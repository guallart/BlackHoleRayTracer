using BlackHoleRayTracer.Background;

namespace BlackHoleRayTracer.Configuration;

public static class ConfigValidation
{
  public static List<string> ValidateCongif()
  {
    IEnumerable<string> EnumerateProblems()
    {
      if (Config.Rs <= 0.0)
        yield return "Scene.Rs must be positive.";

      if (Config.Camera.R <= 1.0)
        yield return $"Camera.R = {Config.Camera.R}*rs is inside or on the event horizon (must be > 1).";

      if (Config.Camera.R <= 1.5)
        yield return $"Camera.R = {Config.Camera.R}*rs is inside the photon sphere (1.5*rs): "
                     + "the image will not correspond to an external observer and most of the field will be shadow.";

      if (Config.Camera.Width <= 0 || Config.Camera.Height <= 0)
        yield return "Camera.Width and Camera.Height must be positive.";

      if (Config.Camera.Fov.Degrees <= 0.0 || Config.Camera.Fov.Degrees >= 180.0)
        yield return "Camera.FovDeg must be between 0 and 180 (exclusive).";

      if (Config.Disk.DiskEnabled && Config.Disk.DiskInner >= Config.Disk.DiskOuter)
        yield return "Scene.DiskInner must be less than Scene.DiskOuter.";

      if (Config.Disk.DiskEnabled && Config.Disk.DiskInner < 1.5)
        yield return $"Scene.DiskInner = {Config.Disk.DiskInner}*rs is inside the photon sphere; "
                     + "no circular orbits exist there and the redshift factor is clipped. The ISCO is at 3*rs.";

      if (Config.Solver.REscape <= Config.Camera.R)
        yield return $"Solver.REscape ({Config.Solver.REscape}*rs) must be greater than Camera.R ({Config.Camera.R}*rs), "
                     + "or all rays will be considered escaped on the first step.";

      if (Config.Shading.Supersample < 1)
        yield return "Shading.Supersample must be at least 1.";

      if (Config.Shading.Exposure <= 0.0)
        yield return "Shading.Exposure must be positive.";

      if (Config.Back.BackType == BackgroundType.Image && Config.Back.BackImagePath == null)
        yield return "A image path must be provided if Back.BackType == BackgroundType.Image.";

      if (Config.Mode == RunMode.Image)
      {
        if (Config.Camera.Width <= 0)
          yield return "Camera.Width must be greater than zero.";

        if (Config.Camera.Height <= 0)
          yield return "Camera.Height must be greater than zero.";

        if (Config.Camera.R < Config.Rs)
          yield return "The camera must be outside the horizon: Camera.R > Rs";
      }

      if (Config.Mode == RunMode.CustomPath)
      {
        if (Config.Video.Frames < 1)
          yield return "Video.Frames must be at least 1.";

        if (Config.Video.Fps < 1)
          yield return "Video.Fps must be at least 1.";

        if (Config.Video.CustomKeyframes.Length == 0)
          yield return "Video.Path = Custom but Video.CustomKeyframes is empty.";

        if (!double.IsNaN(Config.Video.Duration) && Config.Video.Duration <= 0.0)
          yield return "Video.Duration must be positive (or NaN for automatic).";
      }

      if (Config.Mode == RunMode.FreeFall)
      {
        double x0 = Config.Video.X0;
        double y0 = Config.Video.Y0;
        double z0 = Config.Video.Z0;
        double r = Math.Sqrt(x0 * x0 + y0 * y0 + z0 * z0);

        if (r < Config.Rs)
          yield return "Trajectory's inital position is inside the event horizon";
      }
    }

    return EnumerateProblems().ToList();
  }
}
