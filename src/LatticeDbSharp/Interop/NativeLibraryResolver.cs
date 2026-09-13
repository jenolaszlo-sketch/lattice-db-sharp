using System.Reflection;
using System.Runtime.InteropServices;

namespace LatticeDbSharp.Interop;

internal static class NativeLibraryResolver
{
    internal const string LibraryPathEnvironmentVariable = "LATTICEDBSHARP_NATIVE_LIBRARY";

    private static readonly object Sync = new();
    private static bool initialized;
    private static string? resolvedPath;
    private static nint resolvedHandle;

    internal static string? ResolvedPath
    {
        get
        {
            lock (Sync)
            {
                return resolvedPath;
            }
        }
    }

    internal static void EnsureInitialized()
    {
        lock (Sync)
        {
            if (initialized)
            {
                return;
            }

            NativeLibrary.SetDllImportResolver(typeof(NativeMethods).Assembly, Resolve);
            initialized = true;
        }
    }

    internal static string DescribeSearch()
    {
        var explicitPath = Environment.GetEnvironmentVariable(LibraryPathEnvironmentVariable);
        var candidates = GetConventionalCandidates();
        return $"RID '{RuntimeInformation.RuntimeIdentifier}', architecture '{RuntimeInformation.ProcessArchitecture}', " +
            $"explicit path '{explicitPath ?? "<not set>"}', conventional paths [{string.Join(", ", candidates)}]";
    }

    private static nint Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        _ = assembly;
        _ = searchPath;

        if (!string.Equals(libraryName, NativeMethods.LibraryName, StringComparison.Ordinal))
        {
            return nint.Zero;
        }

        lock (Sync)
        {
            // LibraryImport asks the resolver for each unresolved entry point.
            // Keep every entry point on the same implementation for the
            // lifetime of the assembly, even if the environment changes later.
            if (resolvedHandle != nint.Zero)
            {
                return resolvedHandle;
            }

            var explicitPath = Environment.GetEnvironmentVariable(LibraryPathEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(explicitPath))
            {
                if (!Path.IsPathFullyQualified(explicitPath))
                {
                    throw new DllNotFoundException(
                        $"{LibraryPathEnvironmentVariable} must contain an absolute path; received '{explicitPath}'.");
                }

                return LoadExactPath(Path.GetFullPath(explicitPath));
            }

            foreach (var candidate in GetConventionalCandidates())
            {
                if (File.Exists(candidate))
                {
                    return LoadExactPath(candidate);
                }
            }

            throw new DllNotFoundException(
                $"The pinned LatticeDB native library was not found. {DescribeSearch()}. " +
                $"Set {LibraryPathEnvironmentVariable} to an absolute verified library path.");
        }
    }

    private static nint LoadExactPath(string path)
    {
        if (!File.Exists(path))
        {
            throw new DllNotFoundException($"The configured LatticeDB native library '{path}' does not exist.");
        }

        try
        {
            var handle = NativeLibrary.Load(path);
            resolvedPath = path;
            resolvedHandle = handle;
            return handle;
        }
        catch (Exception exception) when (exception is DllNotFoundException or BadImageFormatException)
        {
            throw new DllNotFoundException(
                $"The LatticeDB native library '{path}' could not be loaded for " +
                $"{RuntimeInformation.RuntimeIdentifier}/{RuntimeInformation.ProcessArchitecture}.",
                exception);
        }
    }

    private static IReadOnlyList<string> GetConventionalCandidates()
    {
        var fileName = GetLibraryFileName();
        var baseDirectory = AppContext.BaseDirectory;
        return
        [
            Path.GetFullPath(Path.Combine(baseDirectory, fileName)),
            Path.GetFullPath(Path.Combine(
                baseDirectory,
                "runtimes",
                RuntimeInformation.RuntimeIdentifier,
                "native",
                fileName)),
        ];
    }

    private static string GetLibraryFileName()
    {
        if (OperatingSystem.IsWindows())
        {
            return "lattice.dll";
        }

        if (OperatingSystem.IsMacOS())
        {
            return "liblattice.dylib";
        }

        if (OperatingSystem.IsLinux())
        {
            return "liblattice.so";
        }

        throw new PlatformNotSupportedException(
            $"LatticeDbSharp does not define a native library name for '{RuntimeInformation.OSDescription}'.");
    }
}
