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

/// <summary>An immutable, detached LatticeDB value with structural equality.</summary>
/// <remarks>
/// Lists and vectors are order-sensitive. Maps compare ordinal keys without
/// depending on enumeration order. Floating-point values use the corresponding
/// .NET <see cref="double.Equals(double)"/> and <see cref="float.Equals(float)"/> semantics.
/// </remarks>
public readonly struct LatticeValue : IEquatable<LatticeValue>
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

    /// <summary>Compares values by type and recursively by content.</summary>
    public bool Equals(LatticeValue other)
    {
        if (Type != other.Type)
        {
            return false;
        }

        return Type switch
        {
            LatticeValueType.Null => true,
            LatticeValueType.Bytes => ((byte[])value!).AsSpan().SequenceEqual((byte[])other.value!),
            LatticeValueType.Vector => VectorEquals((float[])value!, (float[])other.value!),
            LatticeValueType.List => ListEquals(
                (IReadOnlyList<LatticeValue>)value!,
                (IReadOnlyList<LatticeValue>)other.value!),
            LatticeValueType.Map => MapEquals(
                (IReadOnlyDictionary<string, LatticeValue>)value!,
                (IReadOnlyDictionary<string, LatticeValue>)other.value!),
            _ => Equals(value, other.value),
        };
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is LatticeValue other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Type);
        switch (Type)
        {
            case LatticeValueType.Bytes:
                foreach (var item in (byte[])value!) hash.Add(item);
                break;
            case LatticeValueType.Vector:
                foreach (var item in (float[])value!) hash.Add(item);
                break;
            case LatticeValueType.List:
                foreach (var item in (IReadOnlyList<LatticeValue>)value!) hash.Add(item);
                break;
            case LatticeValueType.Map:
                // XOR makes the map hash independent of enumeration order.
                var mapHash = 0;
                foreach (var pair in (IReadOnlyDictionary<string, LatticeValue>)value!)
                {
                    mapHash ^= HashCode.Combine(StringComparer.Ordinal.GetHashCode(pair.Key), pair.Value.GetHashCode());
                }
                hash.Add(mapHash);
                break;
            default:
                hash.Add(value);
                break;
        }

        return hash.ToHashCode();
    }

    public static bool operator ==(LatticeValue left, LatticeValue right) => left.Equals(right);

    public static bool operator !=(LatticeValue left, LatticeValue right) => !left.Equals(right);

    private static bool ListEquals(IReadOnlyList<LatticeValue> left, IReadOnlyList<LatticeValue> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var index = 0; index < left.Count; index++)
        {
            if (!left[index].Equals(right[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool VectorEquals(float[] left, float[] right)
    {
        if (left.Length != right.Length)
            return false;
        for (var index = 0; index < left.Length; index++)
        {
            if (!left[index].Equals(right[index]))
                return false;
        }

        return true;
    }

    private static bool MapEquals(
        IReadOnlyDictionary<string, LatticeValue> left,
        IReadOnlyDictionary<string, LatticeValue> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        foreach (var pair in left)
        {
            if (!right.TryGetValue(pair.Key, out var value) || !pair.Value.Equals(value))
            {
                return false;
            }
        }

        return true;
    }

    private T GetValue<T>(LatticeValueType expected)
    {
        if (Type != expected)
        {
            throw new InvalidOperationException($"Value has type {Type}, not {expected}.");
        }

        return (T)value!;
    }
}
