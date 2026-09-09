using System.Runtime.InteropServices;
using System.Text;

namespace LatticeDbSharp.Interop;

internal static class NativeValueConversion
{
    private static readonly Encoding Utf8 = NativeText.Utf8;

    internal static LatticeValue ToManaged(NativeValue value)
    {
        return ToManaged(value, new ConversionBudget());
    }

    private static LatticeValue ToManaged(NativeValue value, ConversionBudget budget)
    {
        return value.Type switch
        {
            NativeValueType.Null => LatticeValue.Null,
            NativeValueType.Boolean => LatticeValue.From(value.BooleanValue != 0),
            NativeValueType.Integer => LatticeValue.From(value.IntegerValue),
            NativeValueType.Float => LatticeValue.From(value.FloatValue),
            NativeValueType.String => LatticeValue.From(ReadString(value.StringValue, budget)),
            NativeValueType.Bytes => LatticeValue.From(ReadBytes(value.BytesValue, budget)),
            NativeValueType.Vector => LatticeValue.From(ReadVector(value.VectorValue, budget)),
            NativeValueType.List => ReadList(value.ListValue, budget),
            NativeValueType.Map => ReadMap(value.MapValue, budget),
            _ => throw new InvalidDataException($"Native value type {(int)value.Type} is not supported."),
        };
    }

    private static string ReadString(NativeStringValue value, ConversionBudget budget)
    {
        return Utf8.GetString(ReadBytes(value, budget));
    }

    private static byte[] ReadBytes(NativeStringValue value, ConversionBudget budget)
    {
        var length = CheckedByteLength(value.Length);
        budget.AddBytes(length);
        if (length == 0)
        {
            return Array.Empty<byte>();
        }

        if (value.Pointer == nint.Zero)
        {
            throw new InvalidDataException("Native value contains a null data pointer with a non-zero length.");
        }

        var bytes = new byte[length];
        Marshal.Copy(value.Pointer, bytes, 0, length);
        return bytes;
    }

    private static float[] ReadVector(NativeVectorValue value, ConversionBudget budget)
    {
        if (value.Dimensions > int.MaxValue)
        {
            throw new InvalidDataException("Native vector dimensions exceed the managed safety limit.");
        }

        var length = (int)value.Dimensions;
        budget.AddBytes(checked((long)length * sizeof(float)));
        if (length == 0)
        {
            return Array.Empty<float>();
        }

        if (value.Pointer == nint.Zero)
        {
            throw new InvalidDataException("Native vector contains a null data pointer with non-zero dimensions.");
        }

        var vector = new float[length];
        Marshal.Copy(value.Pointer, vector, 0, length);
        return vector;
    }

    private static LatticeValue ReadList(nint pointer, ConversionBudget budget)
    {
        if (pointer == nint.Zero)
        {
            throw new InvalidDataException("Native list pointer is null.");
        }

        var list = Marshal.PtrToStructure<NativeListValue>(pointer);
        var length = CheckedCollectionLength(list.Length);
        budget.EnterContainer(length);
        try
        {
            if (length == 0)
            {
                return LatticeValue.From(Array.Empty<LatticeValue>());
            }

            if (list.Items == nint.Zero)
            {
                throw new InvalidDataException("Native list contains a null item pointer with non-zero length.");
            }

            var itemSize = Marshal.SizeOf<NativeValue>();
            var values = new LatticeValue[length];
            for (var index = 0; index < length; index++)
            {
                var item = Marshal.PtrToStructure<NativeValue>(AddOffset(list.Items, index, itemSize));
                values[index] = ToManaged(item, budget);
            }

            return LatticeValue.From(values);
        }
        finally
        {
            budget.ExitContainer();
        }
    }

    private static LatticeValue ReadMap(nint pointer, ConversionBudget budget)
    {
        if (pointer == nint.Zero)
        {
            throw new InvalidDataException("Native map pointer is null.");
        }

        var map = Marshal.PtrToStructure<NativeMapValue>(pointer);
        var length = CheckedCollectionLength(map.Length);
        budget.EnterContainer(length);
        try
        {
            if (length == 0)
            {
                return LatticeValue.From(new Dictionary<string, LatticeValue>(StringComparer.Ordinal));
            }

            if (map.Entries == nint.Zero)
            {
                throw new InvalidDataException("Native map contains a null entry pointer with non-zero length.");
            }

            var entrySize = Marshal.SizeOf<NativeMapEntry>();
            var values = new Dictionary<string, LatticeValue>(length, StringComparer.Ordinal);
            for (var index = 0; index < length; index++)
            {
                var entry = Marshal.PtrToStructure<NativeMapEntry>(AddOffset(map.Entries, index, entrySize));
                var key = Utf8.GetString(ReadBytes(new NativeStringValue
                {
                    Pointer = entry.Key,
                    Length = entry.KeyLength,
                }, budget));
                if (!values.TryAdd(key, ToManaged(entry.Value, budget)))
                {
                    throw new InvalidDataException($"Native map contains duplicate key '{key}'.");
                }
            }

            return LatticeValue.From(values);
        }
        finally
        {
            budget.ExitContainer();
        }
    }

    private static int CheckedByteLength(nuint length)
    {
        if (length > (nuint)int.MaxValue)
        {
            throw new InvalidDataException("Native value length exceeds the managed safety limit.");
        }

        return (int)length;
    }

    private static int CheckedCollectionLength(nuint length)
    {
        if (length > (nuint)NativeValueLimits.MaxCollectionItems)
        {
            throw new InvalidDataException(
                $"Native recursive value exceeded the {NativeValueLimits.MaxCollectionItems:N0}-item safety limit.");
        }

        return (int)length;
    }

    internal static nint AddOffset(nint pointer, int index, int elementSize)
    {
        try
        {
            var offset = checked(index * elementSize);
            return checked(pointer + offset);
        }
        catch (OverflowException exception)
        {
            throw new InvalidDataException("Native recursive value pointer arithmetic overflowed.", exception);
        }
    }

    private sealed class ConversionBudget
    {
        private long copiedBytes;
        private int depth;
        private long itemCount;

        internal void AddBytes(long bytes)
        {
            if (bytes < 0 || copiedBytes > NativeValueLimits.MaxCopiedBytes - bytes)
            {
                throw new InvalidDataException(
                    $"Native recursive value exceeded the {NativeValueLimits.MaxCopiedBytes:N0}-byte safety limit.");
            }

            copiedBytes += bytes;
        }

        internal void EnterContainer(int itemCountForContainer)
        {
            if (depth >= NativeValueLimits.MaxDepth)
            {
                throw new InvalidDataException(
                    $"Native recursive value exceeded the {NativeValueLimits.MaxDepth}-level safety limit.");
            }

            if (itemCountForContainer > NativeValueLimits.MaxCollectionItems - itemCount)
            {
                throw new InvalidDataException(
                    $"Native recursive value exceeded the {NativeValueLimits.MaxCollectionItems:N0}-item safety limit.");
            }

            depth++;
            itemCount += itemCountForContainer;
        }

        internal void ExitContainer()
        {
            depth--;
        }
    }
}
