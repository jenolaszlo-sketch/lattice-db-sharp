using System.Runtime.InteropServices;

namespace LatticeDbSharp.Interop;

internal sealed class NativeValueBuilder : IDisposable
{
    private readonly List<nint> allocations = new();
    private long allocatedBytes;
    private int depth;
    private long itemCount;

    internal NativeValueBuilder(LatticeValue value)
    {
        try
        {
            Root = Build(value);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    internal NativeValue Root { get; }

    public void Dispose()
    {
        for (var index = allocations.Count - 1; index >= 0; index--)
        {
            Marshal.FreeHGlobal(allocations[index]);
        }

        allocations.Clear();
    }

    private NativeValue Build(LatticeValue value)
    {
        var native = new NativeValue { Type = (NativeValueType)value.Type };
        switch (value.Type)
        {
            case LatticeValueType.Null:
                break;
            case LatticeValueType.Boolean:
                native.BooleanValue = value.AsBoolean() ? (byte)1 : (byte)0;
                break;
            case LatticeValueType.Integer:
                native.IntegerValue = value.AsInt64();
                break;
            case LatticeValueType.Float:
                native.FloatValue = value.AsDouble();
                break;
            case LatticeValueType.String:
                native.StringValue = BuildBytes(value.AsString());
                break;
            case LatticeValueType.Bytes:
                native.BytesValue = BuildBytes(value.GetBytesStorage());
                break;
            case LatticeValueType.Vector:
                native.VectorValue = BuildVector(value.GetVectorStorage());
                break;
            case LatticeValueType.List:
                native.ListValue = BuildList(value.AsList());
                break;
            case LatticeValueType.Map:
                native.MapValue = BuildMap(value.AsMap());
                break;
            default:
                throw new NotSupportedException($"Unsupported value type '{value.Type}'.");
        }

        return native;
    }

    private NativeStringValue BuildBytes(string value)
    {
        var byteCount = NativeText.GetByteCount(value, "value");
        EnsureBudget(byteCount);
        return BuildBytesCore(NativeText.Encode(value, "value"));
    }

    private NativeStringValue BuildBytes(byte[] value)
    {
        return BuildBytesCore(value);
    }

    private NativeStringValue BuildBytesCore(ReadOnlySpan<byte> value)
    {
        if (value.Length == 0)
        {
            return default;
        }

        var pointer = Allocate(checked(value.Length));
        Marshal.Copy(value.ToArray(), 0, pointer, value.Length);
        return new NativeStringValue { Pointer = pointer, Length = (nuint)value.Length };
    }

    private NativeVectorValue BuildVector(float[] value)
    {
        if (value.Length == 0)
        {
            return default;
        }

        var pointer = Allocate(checked(value.Length * sizeof(float)));
        Marshal.Copy(value.ToArray(), 0, pointer, value.Length);
        return new NativeVectorValue { Pointer = pointer, Dimensions = (uint)value.Length };
    }

    private nint BuildList(IReadOnlyList<LatticeValue> values)
    {
        EnterContainer(values.Count);
        try
        {
            var listPointer = Allocate(Marshal.SizeOf<NativeListValue>());
            var itemSize = Marshal.SizeOf<NativeValue>();
            var itemsPointer = values.Count == 0
                ? nint.Zero
                : Allocate(checked(itemSize * values.Count));

            for (var index = 0; index < values.Count; index++)
            {
                Write(AddOffset(itemsPointer, index, itemSize), Build(values[index]));
            }

            Write(listPointer, new NativeListValue
            {
                Items = itemsPointer,
                Length = (nuint)values.Count,
            });
            return listPointer;
        }
        finally
        {
            depth--;
        }
    }

    private nint BuildMap(IReadOnlyDictionary<string, LatticeValue> values)
    {
        EnterContainer(values.Count);
        try
        {
            var mapPointer = Allocate(Marshal.SizeOf<NativeMapValue>());
            var entrySize = Marshal.SizeOf<NativeMapEntry>();
            var entriesPointer = values.Count == 0
                ? nint.Zero
                : Allocate(checked(entrySize * values.Count));

            var index = 0;
            foreach (var pair in values)
            {
                var key = BuildBytes(pair.Key);
                Write(AddOffset(entriesPointer, index, entrySize), new NativeMapEntry
                {
                    Key = key.Pointer,
                    KeyLength = key.Length,
                    Value = Build(pair.Value),
                });
                index++;
            }

            Write(mapPointer, new NativeMapValue
            {
                Entries = entriesPointer,
                Length = (nuint)values.Count,
            });
            return mapPointer;
        }
        finally
        {
            depth--;
        }
    }

    private nint Allocate(int byteCount)
    {
        EnsureBudget(byteCount);
        var pointer = Marshal.AllocHGlobal(byteCount);
        allocatedBytes += byteCount;
        allocations.Add(pointer);
        return pointer;
    }

    private void EnterContainer(int itemCountForContainer)
    {
        if (depth >= NativeValueLimits.MaxDepth)
        {
            throw new ArgumentOutOfRangeException(
                nameof(LatticeValue),
                $"Nested values cannot exceed {NativeValueLimits.MaxDepth} levels.");
        }

        if (itemCountForContainer > NativeValueLimits.MaxCollectionItems - itemCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(LatticeValue),
                $"Nested values cannot contain more than {NativeValueLimits.MaxCollectionItems} items.");
        }

        depth++;
        itemCount += itemCountForContainer;
    }

    private void EnsureBudget(long bytes)
    {
        if (bytes < 0 || allocatedBytes > NativeValueLimits.MaxCopiedBytes - bytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(LatticeValue),
                $"Nested values cannot allocate more than {NativeValueLimits.MaxCopiedBytes} bytes.");
        }
    }

    private static nint AddOffset(nint pointer, int index, int elementSize)
    {
        var offset = checked(index * elementSize);
        return checked(pointer + offset);
    }

    private static void Write<T>(nint destination, T value)
        where T : struct
    {
        Marshal.StructureToPtr(value, destination, fDeleteOld: false);
    }
}
