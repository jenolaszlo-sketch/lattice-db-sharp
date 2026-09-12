using System.Runtime.InteropServices;

namespace LatticeDbSharp.Interop;

/// <summary>Copies native identifier arrays into detached managed lists.</summary>
internal static class NativeCollections
{
    internal static IReadOnlyList<LatticeNodeId> ReadNodeIds(nint ids, nuint count)
    {
        if (count == 0)
        {
            return [];
        }

        if (ids == nint.Zero)
        {
            throw new InvalidDataException("Native node enumeration returned no storage.");
        }

        var length = checked((int)count);
        var buffer = new long[length];
        Marshal.Copy(ids, buffer, 0, length);
        var nodes = new LatticeNodeId[length];
        for (var index = 0; index < length; index++)
        {
            nodes[index] = new LatticeNodeId((ulong)buffer[index]);
        }

        return Array.AsReadOnly(nodes);
    }

    internal static IReadOnlyList<LatticeEdgeId> ReadEdgeIds(nint ids, nuint count)
    {
        if (count == 0)
        {
            return [];
        }

        if (ids == nint.Zero)
        {
            throw new InvalidDataException("Native edge enumeration returned no storage.");
        }

        var length = checked((int)count);
        var buffer = new long[length];
        Marshal.Copy(ids, buffer, 0, length);
        var edges = new LatticeEdgeId[length];
        for (var index = 0; index < length; index++)
        {
            edges[index] = new LatticeEdgeId((ulong)buffer[index]);
        }

        return Array.AsReadOnly(edges);
    }

    internal static IReadOnlyList<LatticeFtsHit> ReadFtsHits(nint result)
    {
        var count = checked((int)NativeMethods.FtsResultCount(result));
        var hits = new List<LatticeFtsHit>(count);
        for (var index = 0u; index < (uint)count; index++)
        {
            var error = NativeMethods.FtsResultGet(result, index, out var nodeId, out var score);
            NativeError.ThrowIfFailed(error, "fts/read");
            hits.Add(new LatticeFtsHit(new LatticeNodeId(nodeId), score));
        }

        return hits.AsReadOnly();
    }

    internal static IReadOnlyList<LatticeVectorHit> ReadVectorHits(nint result)
    {
        var count = checked((int)NativeMethods.VectorResultCount(result));
        var hits = new List<LatticeVectorHit>(count);
        for (var index = 0u; index < (uint)count; index++)
        {
            var error = NativeMethods.VectorResultGet(result, index, out var nodeId, out var distance);
            NativeError.ThrowIfFailed(error, "vector/read");
            hits.Add(new LatticeVectorHit(new LatticeNodeId(nodeId), distance));
        }

        return hits.AsReadOnly();
    }
}
