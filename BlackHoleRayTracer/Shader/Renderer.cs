using BlackHoleRayTracer.Background;
using BlackHoleRayTracer.Camera;
using BlackHoleRayTracer.Configuration;
using BlackHoleRayTracer.Core;
using BlackHoleRayTracer.Diagnostics;
using BlackHoleRayTracer.Export;
using BlackHoleRayTracer.Rays;

using ILGPU;
using ILGPU.Runtime;

using System.Diagnostics;
using System.Globalization;

namespace BlackHoleRayTracer.Shader;

public static class Renderer
{
  public static void Render()
  {
    ConfigPrinter.Print();
    Stopwatch watch = Stopwatch.StartNew();

    Console.WriteLine("Setting up.");
    Context context = Context.CreateDefault();
    Accelerator accelerator = context.GetPreferredDevice(preferCPU: false).CreateAccelerator(context);

    int width = Config.Camera.Width;
    int height = Config.Camera.Height;
    int superSamp = Config.Shading.Supersample;
    int pixelCountFull = width * height * superSamp * superSamp;
    int pixelCountReduced = width * height;
    int elementCountFull = pixelCountFull * Config.ChannelsFull;
    int elementCountReduced = pixelCountReduced * Config.ChannelsFull;
    int rgbElementCount = width * height * Config.ChannelsReduced;
    bool isVideoMode = Config.Mode != RunMode.Image;

    CameraPath camPath = new();
    TexelLoader texLoad = new();
    BackgroundSampler bgSampler = new() { Width = texLoad.Width, Height = texLoad.Height };
    byte[] rgb = new byte[rgbElementCount];
    float profileMax = Shader.PeakProfile();
    float duration = camPath.Duration;

    using var statesBuffer = accelerator.Allocate1D<RayState>(pixelCountFull);
    using var resultsBuffer = accelerator.Allocate1D<RayResult>(pixelCountFull);
    using var texelsBuffer = accelerator.Allocate1D(texLoad.Texels);
    using var lutBuffer = accelerator.Allocate1D(Spectrum.BuildLut());
    using var pixelsBuffer = accelerator.Allocate1D<float>(elementCountFull);
    using var pixelsDownsampledBuffer = accelerator.Allocate1D<float>(elementCountReduced);
    using var rgbBuffer = accelerator.Allocate1D<byte>(rgbElementCount);

    var integrator = accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<RayState>>(GeodesicSolver.Integrate);
    var resultBuilder = accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<RayState>, ArrayView<RayResult>, int>(RayResultBuilder.Build);
    var generator = accelerator.LoadAutoGroupedStreamKernel<Index1D, CameraParams, ArrayView<RayState>>(CameraRayGenerator.Generate);
    var shader = accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<RayResult>, ArrayView<float>, ArrayView<float>, ArrayView<float>, BackgroundSampler, float>(Shader.Shade);
    var downSampler = accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<float>, int, int>(DownSampler.DownSample);
    var toneMapper = accelerator.LoadAutoGroupedStreamKernel<Index1D, ArrayView<float>, ArrayView<byte>>(ToneMapper.ToneMap);

    Console.WriteLine("Rendering.");
    int frameCount = isVideoMode ? Config.Video.Frames : 1;

    for (int frame = 0; frame < frameCount; frame++)
    {
      float time = duration * frame / (frameCount - 1f);
      float progress = (float)frame / (frameCount - 1f);
      CameraParams cam = camPath.Sample(time);

      double genMs = Clock.TimeIt(() =>
      {
        generator(cam.PixelCount, cam, statesBuffer.View);
        accelerator.Synchronize();
      });

      double intMs = Clock.TimeIt(() =>
      {
        integrator(cam.PixelCount, statesBuffer.View);
        resultBuilder(cam.PixelCount, statesBuffer.View, resultsBuffer.View, cam.Width);
        accelerator.Synchronize();
      });

      RayResult[] results = resultsBuffer.GetAsArray1D();
      TraceStats stats = TraceStats.From(results, genMs, intMs);
      if (!isVideoMode) stats.Print();

      double shadingMs = Clock.TimeIt(() =>
      {
        shader(cam.PixelCount, resultsBuffer.View, texelsBuffer.View, pixelsBuffer.View, lutBuffer.View, bgSampler, profileMax);
        downSampler(pixelCountReduced, pixelsBuffer.View, pixelsDownsampledBuffer.View, width, height);
        toneMapper(pixelCountReduced, pixelsDownsampledBuffer.View, rgbBuffer.View);
        rgbBuffer.CopyToCPU(rgb);
        Legend.Draw(rgb, stats, cam);
        TrajectoryPlot.Draw(rgb, camPath.Keys, progress);
        accelerator.Synchronize();
      });

      string path = FramePath(frame);
      ImageWriter.Write(path, rgb);

      double totalFrameMS = genMs + intMs + shadingMs;
      double totalElapsedSeconds = watch.Elapsed.TotalSeconds;
      ConsoleLog.LogFrame(frame, stats, totalFrameMS, totalElapsedSeconds);
    }

    if (isVideoMode)
    {
      RenderVideo();
    }
    else
    {
      if (File.Exists(Paths.OutputPath))
        File.Delete(Paths.OutputPath);
      File.Copy(FramePath(0), Paths.OutputPath);
    }

    accelerator.Dispose();
    context.Dispose();
  }

  private static string FramePath(int frame)
  {
    string frameExtension = Config.Output.FrameExtension.ToString().ToLower();
    string fileName = $"frame_{frame.ToString("D5", CultureInfo.InvariantCulture)}.{frameExtension}";
    return Path.Combine(Paths.FramesDirectory, fileName);
  }

  private static void RenderVideo()
  {
    Console.WriteLine("Video assembly.");

    int fps = Config.Video.Fps;
    string framePattern = Path.Combine(Paths.FramesDirectory, $"frame_%05d.{Paths.FrameExtension}");
    string outPath = Paths.OutputPath;

    string ffmpegCommand =
        $"ffmpeg -y -framerate {fps} -i \"{framePattern}\" -c:v libx264 -pix_fmt yuv420p -crf 17 \"{outPath}\"";

    if (ShellCommand.RunCommand(ffmpegCommand) != 0)
      throw new InvalidOperationException("FFmpeg failed to generate a video.");
  }
}