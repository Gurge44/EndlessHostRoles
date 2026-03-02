/*#nullable enable
using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace EHR.Modules;

internal static class EmbeddedDeps
{
    private static bool _installed;
    private static readonly BepInEx.Logging.ManualLogSource Log =
        BepInEx.Logging.Logger.CreateLogSource("EHR.EmbeddedLoader");

    public static void Install()
    {
        if (_installed) return;
        _installed = true;

        Log.LogInfo("Embedded dependency resolver installed.");

        AppDomain.CurrentDomain.AssemblyResolve += Resolve;
    }

    private static Assembly? Resolve(object? sender, ResolveEventArgs args)
    {
        var requestedName = new AssemblyName(args.Name).Name + ".dll";

        var asm = Assembly.GetExecutingAssembly();
        var resName = asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(requestedName, StringComparison.OrdinalIgnoreCase));

        if (resName == null)
            return null;

        using var stream = asm.GetManifestResourceStream(resName);
        if (stream == null)
            return null;

        using var ms = new MemoryStream();
        stream.CopyTo(ms);

        var loaded = Assembly.Load(ms.ToArray());

        Log.LogInfo($"Loaded embedded assembly: {loaded.GetName().Name}");

        return loaded;
    }
}
*/