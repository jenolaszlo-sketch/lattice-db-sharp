using System.Runtime.InteropServices;

namespace LatticeDBSharp.Interop;

[StructLayout(LayoutKind.Sequential)]
internal struct NativeOpenOptionsV4
{
    internal nuint StructSize;
    internal byte Create;
    internal byte ReadOnly;
    internal uint CacheSizeMb;
    internal uint PageSize;
    internal byte EnableVector;
    internal ushort VectorDimensions;
    internal byte EnableWal;
    internal byte EnableAdjacencyCache;
    internal byte Lock;

    internal static NativeOpenOptionsV4 CreateDefault(bool create, bool readOnly)
    {
        return new NativeOpenOptionsV4
        {
            StructSize = (nuint)Marshal.SizeOf<NativeOpenOptionsV4>(),
            Create = Convert.ToByte(create),
            ReadOnly = Convert.ToByte(readOnly),
            CacheSizeMb = 100,
            PageSize = 4096,
            EnableVector = 0,
            VectorDimensions = 128,
            EnableWal = 1,
            EnableAdjacencyCache = 0,
            Lock = 1,
        };
    }
}
