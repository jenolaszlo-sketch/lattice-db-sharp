using System.Runtime.InteropServices;

namespace LatticeDBSharp.Interop;

internal enum NativeValueType
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

[StructLayout(LayoutKind.Sequential)]
internal struct NativeStringValue
{
    internal nint Pointer;
    internal nuint Length;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativeVectorValue
{
    internal nint Pointer;
    internal uint Dimensions;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativeListValue
{
    internal nint Items;
    internal nuint Length;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativeMapValue
{
    internal nint Entries;
    internal nuint Length;
}

[StructLayout(LayoutKind.Sequential)]
internal struct NativeMapEntry
{
    internal nint Key;
    internal nuint KeyLength;
    internal NativeValue Value;
}

[StructLayout(LayoutKind.Explicit, Size = 24)]
internal struct NativeValue
{
    [FieldOffset(0)]
    internal NativeValueType Type;

    [FieldOffset(8)]
    internal byte BooleanValue;

    [FieldOffset(8)]
    internal long IntegerValue;

    [FieldOffset(8)]
    internal double FloatValue;

    [FieldOffset(8)]
    internal NativeStringValue StringValue;

    [FieldOffset(8)]
    internal NativeStringValue BytesValue;

    [FieldOffset(8)]
    internal NativeVectorValue VectorValue;

    [FieldOffset(8)]
    internal nint ListValue;

    [FieldOffset(8)]
    internal nint MapValue;
}
