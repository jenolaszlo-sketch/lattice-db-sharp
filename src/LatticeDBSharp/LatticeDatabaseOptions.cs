using LatticeDBSharp.Interop;

namespace LatticeDBSharp;

/// <summary>Options used when opening a LatticeDB database.</summary>
public sealed class LatticeDatabaseOptions
{
    /// <summary>Creates the database when it does not exist.</summary>
    public bool Create { get; init; }

    /// <summary>Opens the database without mutation support.</summary>
    public bool ReadOnly { get; init; }

    /// <summary>Gets the cache budget in MiB.</summary>
    public uint CacheSizeMb { get; init; } = 100;

    /// <summary>Gets the database page size.</summary>
    public uint PageSize { get; init; } = 4096;

    /// <summary>Enables vector storage.</summary>
    public bool EnableVector { get; init; }

    /// <summary>Gets the vector dimensions used when vector storage is enabled.</summary>
    public ushort VectorDimensions { get; init; } = 128;

    /// <summary>Enables write-ahead logging.</summary>
    public bool EnableWal { get; init; } = true;

    /// <summary>Enables the in-memory adjacency cache.</summary>
    public bool EnableAdjacencyCache { get; init; }

    /// <summary>Enables upstream file locking.</summary>
    public bool Lock { get; init; } = true;

    internal NativeOpenOptionsV4 ToNative()
    {
        if (CacheSizeMb == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(CacheSizeMb), "Cache size must be positive.");
        }

        if (PageSize == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(PageSize), "Page size must be positive.");
        }

        if (EnableVector && (VectorDimensions is < 1 or > 4096))
        {
            throw new ArgumentOutOfRangeException(
                nameof(VectorDimensions),
                "Vector dimensions must be between 1 and 4096.");
        }

        var native = NativeOpenOptionsV4.CreateDefault(Create, ReadOnly);
        native.CacheSizeMb = CacheSizeMb;
        native.PageSize = PageSize;
        native.EnableVector = Convert.ToByte(EnableVector);
        native.VectorDimensions = VectorDimensions;
        native.EnableWal = Convert.ToByte(EnableWal);
        native.EnableAdjacencyCache = Convert.ToByte(EnableAdjacencyCache);
        native.Lock = Convert.ToByte(Lock);
        return native;
    }
}
