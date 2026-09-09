using LatticeDbSharp.Interop;

namespace LatticeDbSharp;

/// <summary>Identifies the native query pipeline stage that failed.</summary>
public enum LatticeQueryStage
{
    None = 0,
    Parse = 1,
    Semantic = 2,
    Plan = 3,
    Execution = 4,
}

/// <summary>Describes a Cypher preparation, binding, or execution failure.</summary>
public sealed class LatticeQueryException : LatticeException
{
    internal LatticeQueryException(
        string operation,
        NativeErrorCode error,
        string message,
        string? queryErrorCode,
        LatticeQueryStage stage,
        int? line,
        int? column,
        int? length)
        : base(operation, error, message)
    {
        QueryErrorCode = queryErrorCode;
        Stage = stage;
        Line = line;
        Column = column;
        Length = length;
    }

    /// <summary>The native diagnostic code, when one was provided.</summary>
    public string? QueryErrorCode { get; }

    /// <summary>The native diagnostic stage.</summary>
    public LatticeQueryStage Stage { get; }

    /// <summary>The 1-based source line, when available.</summary>
    public int? Line { get; }

    /// <summary>The 1-based source column, when available.</summary>
    public int? Column { get; }

    /// <summary>The source token length, when available.</summary>
    public int? Length { get; }
}
