namespace LatticeDBSharp.Interop;

/// <summary>
/// Conservative limits applied while translating recursive native values. They
/// protect the managed process from corrupt or unexpectedly deep native data.
/// </summary>
internal static class NativeValueLimits
{
    internal const int MaxDepth = 64;
    internal const int MaxCollectionItems = 1_000_000;
    internal const long MaxCopiedBytes = 64L * 1024 * 1024;
    internal const int MaxNativeTextBytes = 1 * 1024 * 1024;
}
