using BlackHoleRayTracer.Configuration;

using System.Reflection;
using System.Text;

namespace BlackHoleRayTracer.Diagnostics;

public static class ConfigPrinter
{
  /// <summary>
  /// Reflects over the Config class (and any nested static classes) 
  /// and prints every public const/static field and property with its current value.
  /// </summary>
  public static void Print()
  {
    Console.WriteLine("===== Config =====");
    PrintType(typeof(Config), 0);
    Console.WriteLine();
  }

  private static void PrintType(Type type, int indent)
  {
    string pad = new string(' ', indent * 2);

    // Fields (covers 'const' and 'static readonly' members)
    var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
    foreach (var field in fields)
    {
      object? value = field.GetValue(null);
      Console.WriteLine($"{pad}{field.Name} = {FormatValue(value)}");
    }

    // Properties (in case any config values are exposed as static properties)
    var props = type.GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
    foreach (var prop in props)
    {
      object? value = prop.GetValue(null);
      Console.WriteLine($"{pad}{prop.Name} = {FormatValue(value)}");
    }

    // Recurse into nested static classes (Camera, Solver, Shading, Disk, Back, Output, Video, etc.)
    var nestedTypes = type.GetNestedTypes(BindingFlags.Public | BindingFlags.Static)
        .Where(t => t.IsAbstract && t.IsSealed); // "static class" compiles to abstract sealed
    foreach (var nested in nestedTypes)
    {
      Console.WriteLine($"{pad}[{nested.Name}]");
      PrintType(nested, indent + 1);
    }
  }

  private static string FormatValue(object? value)
  {
    if (value is null)
      return "null";

    // Arrays (e.g. CustomKeyframes)
    if (value is Array array)
    {
      var sb = new StringBuilder();
      sb.Append('[');
      for (int i = 0; i < array.Length; i++)
      {
        if (i > 0) sb.Append(", ");
        sb.Append(array.GetValue(i));
      }
      sb.Append(']');
      return sb.ToString();
    }

    return value.ToString() ?? "null";
  }
}
