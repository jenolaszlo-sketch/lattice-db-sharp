using System.Runtime.InteropServices;
using LatticeDbSharp.Interop;

namespace LatticeDbSharp;

/// <summary>
/// Deterministic token-hash embeddings computed locally with no model or
/// network. These capture lexical similarity, not semantic meaning: use them
/// for offline fuzzy matching and near-duplicate detection, never as evidence
/// that two texts mean the same thing. Model-backed embeddings belong behind
/// the host's model gateway, not this binding.
/// </summary>
public static class LatticeHashEmbeddings
{
    /// <summary>Hashes text into a fixed-dimension vector. Dimensions must be positive.</summary>
    public static float[] HashEmbed(string text, int dimensions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        if (dimensions < 1 || dimensions > ushort.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(dimensions), "Dimensions must be between 1 and 65535.");
        }

        NativeText.Validate(text, nameof(text));
        _ = LatticeNative.Version;
        var error = NativeMethods.HashEmbed(
            text,
            (nuint)NativeText.GetByteCount(text, nameof(text)),
            checked((ushort)dimensions),
            out var vector,
            out var dimensionsOut);
        NativeError.ThrowIfFailed(error, "embedding/hash");
        try
        {
            if (vector == nint.Zero)
            {
                throw new LatticeException("embedding/hash", NativeErrorCode.Error);
            }

            var count = checked((int)dimensionsOut);
            var values = new float[count];
            Marshal.Copy(vector, values, 0, count);
            return values;
        }
        finally
        {
            if (vector != nint.Zero)
            {
                NativeMethods.FreeHashEmbed(vector, dimensionsOut);
            }
        }
    }
}
