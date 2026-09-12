using System.Collections.ObjectModel;
using LatticeDbSharp.Interop;

namespace LatticeDbSharp;

/// <summary>Identifies the detached value shape returned by LatticeDB.</summary>
public enum LatticeValueType
{
    Null = 0,
    Boolean = 1,
    Integer = 2,
    Float = 3,
    String = 4,
    Bytes = 5,
    Vector = 6,
    List = 7,
    Map = 8,
}

/// <summary>An immutable, detached LatticeDB value.</summary>
public readonly struct LatticeValue
{
    private readonly object? value;

    private LatticeValue(LatticeValueType type, object? value)
    {
        Type = type;
        this.value = value;
    }

    /// <summary>The value's native shape.</summary>
    public LatticeValueType Type { get; }

    /// <summary>The native null value.</summary>
    public static LatticeValue Null => default;

    /// <summary>Creates a Boolean value.</summary>
    public static LatticeValue From(bool value) => new(LatticeValueType.Boolean, value);

    /// <summary>Creates an integer value.</summary>
    public static LatticeValue From(long value) => new(LatticeValueType.Integer, value);

    /// <summary>Creates a floating-point value.</summary>
    public static LatticeValue From(double value) => new(LatticeValueType.Float, value);

    /// <summary>Creates a string value.</summary>
    public static LatticeValue From(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        NativeText.Validate(value, nameof(value));
        return new LatticeValue(LatticeValueType.String, value);
    }

    /// <summary>Creates a defensive copy holding blob bytes.</summary>
    public static LatticeValue From(byte[] value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new LatticeValue(LatticeValueType.Bytes, value.ToArray());
    }

    /// <summary>Creates a blob value from a defensive copy.</summary>
    public static LatticeValue From(ReadOnlyMemory<byte> value) =>
        new(LatticeValueType.Bytes, value.ToArray());

    /// <summary>Creates a vector value from a defensive copy.</summary>
    public static LatticeValue From(ReadOnlyMemory<float> value) =>
        new(LatticeValueType.Vector, value.ToArray());

    /// <summary>Creates a list value from a defensive copy.</summary>
    public static LatticeValue From(IReadOnlyList<LatticeValue> value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new LatticeValue(
            LatticeValueType.List,
            Array.AsReadOnly(value.ToArray()));
    }

    /// <summary>Creates a map value from a defensive copy with ordinal keys.</summary>
    public static LatticeValue From(IReadOnlyDictionary<string, LatticeValue> value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var copy = new Dictionary<string, LatticeValue>(StringComparer.Ordinal);
        foreach (var pair in value)
        {
            ArgumentNullException.ThrowIfNull(pair.Key);
            NativeText.Validate(pair.Key, nameof(value));
            copy.Add(pair.Key, pair.Value);
        }

        return new LatticeValue(
            LatticeValueType.Map,
            new ReadOnlyDictionary<string, LatticeValue>(copy));
    }

    /// <summary>Reads the value as a Boolean.</summary>
    public bool AsBoolean() => GetValue<bool>(LatticeValueType.Boolean);

    /// <summary>Reads the value as an integer.</summary>
    public long AsInt64() => GetValue<long>(LatticeValueType.Integer);

    /// <summary>Reads the value as a float.</summary>
    public double AsDouble() => GetValue<double>(LatticeValueType.Float);

    /// <summary>Reads the value as a string.</summary>
    public string AsString() => GetValue<string>(LatticeValueType.String);

    /// <summary>Reads a defensive copy of the blob bytes.</summary>
    public ReadOnlyMemory<byte> AsBytes() => GetValue<byte[]>(LatticeValueType.Bytes).ToArray();

    /// <summary>Reads a defensive copy of the vector.</summary>
    public ReadOnlyMemory<float> AsVector() => GetValue<float[]>(LatticeValueType.Vector).ToArray();

    /// <summary>Reads the value as a list.</summary>
    public IReadOnlyList<LatticeValue> AsList() => GetValue<IReadOnlyList<LatticeValue>>(LatticeValueType.List);

    /// <summary>Reads the value as a map.</summary>
    public IReadOnlyDictionary<string, LatticeValue> AsMap() =>
        GetValue<IReadOnlyDictionary<string, LatticeValue>>(LatticeValueType.Map);

    internal object? RawValue => value;

    internal byte[] GetBytesStorage() => GetValue<byte[]>(LatticeValueType.Bytes);

    internal float[] GetVectorStorage() => GetValue<float[]>(LatticeValueType.Vector);

    private T GetValue<T>(LatticeValueType expected)
    {
        if (Type != expected)
        {
            throw new InvalidOperationException($"Value has type {Type}, not {expected}.");
        }

        return (T)value!;
    }
}

