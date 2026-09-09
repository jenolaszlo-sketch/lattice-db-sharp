using System.Reflection;
using System.Runtime.InteropServices;
using LatticeDbSharp.Interop;

namespace LatticeDbSharp;

/// <summary>Reports the native contract and loaded LatticeDB implementation.</summary>
public static class LatticeNative
{
    /// <summary>The only upstream native version supported by this assembly.</summary>
    public static string PinnedVersion => GetAssemblyMetadata("LatticeDB.NativeVersion");

    /// <summary>The immutable upstream commit used to derive this binding.</summary>
    public static string PinnedCommit => GetAssemblyMetadata("LatticeDB.NativeCommit");

    /// <summary>The absolute path of the native library after it has been loaded.</summary>
    public static string? LibraryPath => NativeLibraryResolver.ResolvedPath;

    /// <summary>Gets whether the pinned native library can be loaded and is compatible.</summary>
    public static bool IsAvailable
    {
        get
        {
            try
            {
                _ = Version;
                return true;
            }
            catch (LatticeNativeException)
            {
                return false;
            }
        }
    }

    /// <summary>Gets the version reported by the loaded native library.</summary>
    /// <exception cref="LatticeNativeLoadException">The native library could not be loaded.</exception>
    /// <exception cref="LatticeNativeCompatibilityException">The library reports a different version.</exception>
    public static string Version
    {
        get
        {
            nint versionPointer;
            try
            {
                NativeLibraryResolver.EnsureInitialized();
                versionPointer = NativeMethods.GetVersion();
            }
            catch (Exception exception) when (
                exception is DllNotFoundException or
                BadImageFormatException or
                EntryPointNotFoundException or
                PlatformNotSupportedException)
            {
                throw new LatticeNativeLoadException(
                    $"Unable to load LatticeDB {PinnedVersion}. {NativeLibraryResolver.DescribeSearch()}.",
                    exception);
            }

            string? version;
            try
            {
                version = NativeText.DecodeNullTerminated(versionPointer);
            }
            catch (InvalidDataException exception)
            {
                throw new LatticeNativeCompatibilityException(
                    "The loaded LatticeDB library returned invalid UTF-8 for its version.",
                    exception);
            }
            if (string.IsNullOrWhiteSpace(version))
            {
                throw new LatticeNativeCompatibilityException("The loaded LatticeDB library returned no version.");
            }

            if (!string.Equals(version, PinnedVersion, StringComparison.Ordinal))
            {
                throw new LatticeNativeCompatibilityException(
                    $"LatticeDbSharp requires native version {PinnedVersion}, but the loaded library reports {version}.");
            }

            return version;
        }
    }

    private static string GetAssemblyMetadata(string key)
    {
        return typeof(LatticeNative).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => string.Equals(attribute.Key, key, StringComparison.Ordinal))
            .Value!;
    }
}

/// <summary>Base class for failures at the managed/native compatibility boundary.</summary>
public abstract class LatticeNativeException : Exception
{
    private protected LatticeNativeException(string message)
        : base(message)
    {
    }

    private protected LatticeNativeException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>The native library could not be located or loaded.</summary>
public sealed class LatticeNativeLoadException : LatticeNativeException
{
    internal LatticeNativeLoadException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>The loaded native library does not implement the pinned contract.</summary>
public sealed class LatticeNativeCompatibilityException : LatticeNativeException
{
    internal LatticeNativeCompatibilityException(string message)
        : base(message)
    {
    }

    internal LatticeNativeCompatibilityException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
