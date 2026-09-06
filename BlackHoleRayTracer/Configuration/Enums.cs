namespace BlackHoleRayTracer.Configuration;

public enum RunMode
{
  /// <summary>A single image.</summary>
  Image,
  /// <summary>A sequence of custom-defined frames to assemble into a video.</summary>
  CustomPath,
  /// <summary>The positions of the camera are taken from a freefall trajectory.</summary>
  FreeFall,
}

/// <summary>Format of images exported.</summary>
public enum ImageExtension { Png, Ppm }
