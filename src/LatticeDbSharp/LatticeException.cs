using LatticeDbSharp.Interop;

namespace LatticeDbSharp;

/// <summary>Base class for errors reported by the LatticeDB engine.</summary>
public class LatticeException : Exception
{
    internal LatticeException(string operation, NativeErrorCode error)
        : base(CreateMessage(operation, error))
    {
        Operation = operation;
        NativeErrorCode = (int)error;
    }

    internal LatticeException(string operation, NativeErrorCode error, string message)
        : base(message)
    {
        Operation = operation;
        NativeErrorCode = (int)error;
    }

    /// <summary>The wrapper operation that failed.</summary>
    public string Operation { get; }

    /// <summary>The original numeric error code returned by the native engine.</summary>
    public int NativeErrorCode { get; }

    private static string CreateMessage(string operation, NativeErrorCode error)
    {
        var pointer = NativeMethods.GetErrorMessage(error);
        string nativeMessage;
        try
        {
            nativeMessage = NativeText.DecodeNullTerminated(pointer) ?? "Unknown native error";
        }
        catch (InvalidDataException)
        {
            nativeMessage = "Native error message contained invalid UTF-8";
        }

        return $"LatticeDB operation '{operation}' failed with {(int)error}: {nativeMessage}.";
    }
}

/// <summary>Reports a batch insertion failure and the native progress count.</summary>
public sealed class LatticeBatchInsertException : LatticeException
{
    internal LatticeBatchInsertException(
        string operation,
        NativeErrorCode error,
        int completedCount,
        int requestedCount)
        : base(operation, error)
    {
        CompletedCount = completedCount;
        RequestedCount = requestedCount;
    }

    /// <summary>
    /// The progress count returned by native code when the batch failed. Partial
    /// native side effects can exceed this count, so the transaction still requires rollback.
    /// </summary>
    public int CompletedCount { get; }

    /// <summary>The number of nodes requested by the batch.</summary>
    public int RequestedCount { get; }
}

internal static class NativeError
{
    internal static void ThrowIfFailed(NativeErrorCode error, string operation)
    {
        if (error != NativeErrorCode.Ok)
        {
            throw new LatticeException(operation, error);
        }
    }
}
