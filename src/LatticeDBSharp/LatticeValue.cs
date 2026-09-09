using System.Collections.ObjectModel;
using LatticeDBSharp.Interop;

namespace LatticeDBSharp;

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

    public static LatticeValue From(bool value) => new(LatticeValueType.Boolean, value);

    public static LatticeValue From(long value) => new(LatticeValueType.Integer, value);

    public static LatticeValue From(double value) => new(LatticeValueType.Float, value);

    public static LatticeValue From(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        NativeText.Validate(value, nameof(value));
        return new LatticeValue(LatticeValueType.String, value);
    }

    public static LatticeValue From(byte[] value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new LatticeValue(LatticeValueType.Bytes, value.ToArray());
    }

    public static LatticeValue From(ReadOnlyMemory<byte> value) =>
        new(LatticeValueType.Bytes, value.ToArray());

    public static LatticeValue From(ReadOnlyMemory<float> value) =>
        new(LatticeValueType.Vector, value.ToArray());

    public static LatticeValue From(IReadOnlyList<LatticeValue> value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new LatticeValue(
            LatticeValueType.List,
            Array.AsReadOnly(value.ToArray()));
    }

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

    public bool AsBoolean() => GetValue<bool>(LatticeValueType.Boolean);

    public long AsInt64() => GetValue<long>(LatticeValueType.Integer);

    public double AsDouble() => GetValue<double>(LatticeValueType.Float);

    public string AsString() => GetValue<string>(LatticeValueType.String);

    public ReadOnlyMemory<byte> AsBytes() => GetValue<byte[]>(LatticeValueType.Bytes).ToArray();

    public ReadOnlyMemory<float> AsVector() => GetValue<float[]>(LatticeValueType.Vector).ToArray();

    public IReadOnlyList<LatticeValue> AsList() => GetValue<IReadOnlyList<LatticeValue>>(LatticeValueType.List);

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
