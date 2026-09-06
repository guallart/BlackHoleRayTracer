namespace BlackHoleRayTracer.Rays;

/// <summary>Termination status of a geodesic.</summary>
public enum RayStatus : byte
{
  Active = 0,
  Absorbed = 1,
  Escaped = 2,
  HitDisk = 3,
  MaxStepsReached = 4
}
