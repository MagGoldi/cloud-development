using System.Reflection;
using System.Runtime.CompilerServices;

namespace ApiFunction;

/// <summary>
/// Инициализатор модуля — регистрирует обработчик AssemblyResolve до обращения к любому классу.
/// </summary>
public static class ModuleInit
{
    [ModuleInitializer]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2255")]
    public static void Initialize()
    {
        AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;
    }

    private static Assembly? ResolveAssembly(object? sender, ResolveEventArgs args)
    {
        try
        {
            var name = new AssemblyName(args.Name).Name;
            if (string.IsNullOrEmpty(name)) return null;

            var assemblyDir = Path.GetDirectoryName(typeof(ModuleInit).Assembly.Location) ?? "";
            var dllPath = Path.Combine(assemblyDir, name + ".dll");

            if (File.Exists(dllPath))
            {
                Console.WriteLine($"[ModuleInit] Resolved {name} from {dllPath}");
                return Assembly.LoadFrom(dllPath);
            }

            Console.WriteLine($"[ModuleInit] Could not resolve {name} (looked in {assemblyDir})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ModuleInit] ResolveAssembly error: {ex.Message}");
        }

        return null;
    }
}
