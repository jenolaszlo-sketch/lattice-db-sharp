using System.Runtime.InteropServices;
using LatticeDbSharp.Interop;

namespace LatticeDbSharp;

/// <summary>Identifies the mutation capability of a transaction.</summary>
public enum LatticeTransactionMode
{
    /// <summary>A stable read snapshot.</summary>
    ReadOnly = 0,

    /// <summary>A transaction that may read and mutate.</summary>
    ReadWrite = 1,
}

/// <summary>An explicit LatticeDB transaction with deterministic completion.</summary>
public sealed class LatticeTransaction : IDisposable
{
    private readonly object gate = new();
    private readonly LatticeDatabase database;
    private readonly SafeLatticeTransactionHandle handle;
    private bool completed;

    internal LatticeTransaction(
        LatticeDatabase database,
        SafeLatticeTransactionHandle handle,
        LatticeTransactionMode mode)
    {
        this.database = database;
        this.handle = handle;
        Mode = mode;
    }

    /// <summary>The transaction's fixed access mode.</summary>
    public LatticeTransactionMode Mode { get; }

    internal LatticeDatabase Database => database;

    /// <summary>Gets whether commit, rollback, or disposal has completed this transaction.</summary>
    public bool IsCompleted
    {
        get
        {
            lock (gate)
            {
                return completed;
            }
        }
    }

    /// <summary>Creates a node and returns its stable native identifier.</summary>
    public LatticeNodeId CreateNode(string? label = null)
    {
        lock (gate)
        {
            EnsureActive();
            EnsureWritable();
            ValidateLabel(label, nameof(label));
            var error = NativeMethods.CreateNode(handle.DangerousGetHandle(), label, out var nodeId);
            NativeError.ThrowIfFailed(error, "node/create");
            return new LatticeNodeId(nodeId);
        }
    }

    /// <summary>Adds a label to an existing node.</summary>
    public void AddLabel(LatticeNodeId nodeId, string label)
    {
        lock (gate)
        {
            EnsureActive();
            EnsureWritable();
            ArgumentException.ThrowIfNullOrWhiteSpace(label);
            ValidateLabel(label, nameof(label));
            var error = NativeMethods.AddNodeLabel(handle.DangerousGetHandle(), nodeId.Value, label);
            NativeError.ThrowIfFailed(error, "node/add-label");
        }
    }

    /// <summary>Removes a label from an existing node.</summary>
    public void RemoveLabel(LatticeNodeId nodeId, string label)
    {
        lock (gate)
        {
            EnsureActive();
            EnsureWritable();
            ArgumentException.ThrowIfNullOrWhiteSpace(label);
            ValidateLabel(label, nameof(label));
            var error = NativeMethods.RemoveNodeLabel(handle.DangerousGetHandle(), nodeId.Value, label);
            NativeError.ThrowIfFailed(error, "node/remove-label");
        }
    }

    /// <summary>Deletes a node and its native graph data in this transaction.</summary>
    public void DeleteNode(LatticeNodeId nodeId)
    {
        lock (gate)
        {
            EnsureActive();
            EnsureWritable();
            var error = NativeMethods.DeleteNode(handle.DangerousGetHandle(), nodeId.Value);
            NativeError.ThrowIfFailed(error, "node/delete");
        }
    }

    /// <summary>Gets the labels visible on a node in their native order.</summary>
    public IReadOnlyList<string> GetLabels(LatticeNodeId nodeId)
    {
        lock (gate)
        {
            EnsureActive();
            var error = NativeMethods.GetNodeLabels(
                handle.DangerousGetHandle(),
                nodeId.Value,
                out var labelsPointer);
            NativeError.ThrowIfFailed(error, "node/get-labels");
            try
            {
                return DecodeLabels(labelsPointer);
            }
            finally
            {
                if (labelsPointer != nint.Zero)
                {
                    NativeMethods.FreeString(labelsPointer);
                }
            }
        }
    }

    /// <summary>Sets a detached property value on a node.</summary>
    public void SetProperty(LatticeNodeId nodeId, string key, LatticeValue value)
    {
        lock (gate)
        {
            EnsureActive();
            EnsureWritable();
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ValidateNativeString(key, nameof(key));
            using var nativeValue = new NativeValueBuilder(value);
            var root = nativeValue.Root;
            var error = NativeMethods.SetNodeProperty(
                handle.DangerousGetHandle(),
                nodeId.Value,
                key,
                in root);
            NativeError.ThrowIfFailed(error, "node/set-property");
        }
    }

    /// <summary>Gets a detached property value, or <see langword="false"/> when absent.</summary>
    public bool TryGetProperty(LatticeNodeId nodeId, string key, out LatticeValue value)
    {
        lock (gate)
        {
            EnsureActive();
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ValidateNativeString(key, nameof(key));
            var error = NativeMethods.GetNodeProperty(
                handle.DangerousGetHandle(),
                nodeId.Value,
                key,
                out var nativeValue);
            if (error == NativeErrorCode.NotFound)
            {
                value = LatticeValue.Null;
                return false;
            }

            NativeError.ThrowIfFailed(error, "node/get-property");
            try
            {
                value = NativeValueConversion.ToManaged(nativeValue);
                return true;
            }
            finally
            {
                NativeMethods.FreeValue(ref nativeValue);
            }
        }
    }

    /// <summary>Gets a detached property value, throwing when the key is absent.</summary>
    public LatticeValue GetProperty(LatticeNodeId nodeId, string key)
    {
        if (!TryGetProperty(nodeId, key, out var value))
        {
            throw new KeyNotFoundException($"Node {nodeId.Value} has no property named '{key}'.");
        }

        return value;
    }

    /// <summary>Sets the configured native vector for a node.</summary>
    public void SetVector(LatticeNodeId nodeId, ReadOnlyMemory<float> vector)
    {
        lock (gate)
        {
            EnsureActive();
            EnsureWritable();
            if (vector.Length == 0)
            {
                throw new ArgumentException("A vector must contain at least one dimension.", nameof(vector));
            }

            var values = vector.ToArray();
            var pinned = GCHandle.Alloc(values, GCHandleType.Pinned);
            try
            {
                var error = NativeMethods.SetNodeVector(
                    handle.DangerousGetHandle(),
                    nodeId.Value,
                    null,
                    pinned.AddrOfPinnedObject(),
                    checked((uint)values.Length));
                NativeError.ThrowIfFailed(error, "node/set-vector");
            }
            finally
            {
                pinned.Free();
            }
        }
    }

    /// <summary>Returns every node id carrying a label in this snapshot. Unknown labels yield an empty list.</summary>
    public IReadOnlyList<LatticeNodeId> GetNodesByLabel(string label)
    {
        lock (gate)
        {
            EnsureActive();
            ArgumentException.ThrowIfNullOrWhiteSpace(label);
            ValidateNativeString(label, nameof(label));
            var error = NativeMethods.GetNodesByLabelTransaction(
                handle.DangerousGetHandle(),
                label,
                (nuint)NativeText.GetByteCount(label, nameof(label)),
                out var ids,
                out var count);
            NativeError.ThrowIfFailed(error, "node/list-by-label");
            try
            {
                return NativeCollections.ReadNodeIds(ids, count);
            }
            finally
            {
                if (ids != nint.Zero)
                {
                    NativeMethods.FreeNodeIds(ids, count);
                }
            }
        }
    }

    /// <summary>Returns every node id visible in this snapshot.</summary>
    public IReadOnlyList<LatticeNodeId> GetAllNodes()
    {
        lock (gate)
        {
            EnsureActive();
            var error = NativeMethods.GetAllNodesTransaction(
                handle.DangerousGetHandle(),
                out var ids,
                out var count);
            NativeError.ThrowIfFailed(error, "node/list-all");
            try
            {
                return NativeCollections.ReadNodeIds(ids, count);
            }
            finally
            {
                if (ids != nint.Zero)
                {
                    NativeMethods.FreeNodeIds(ids, count);
                }
            }
        }
    }

    /// <summary>
    /// Finds visible node ids through an explicit label/property equality
    /// index. Fails when no such index exists rather than scanning.
    /// <paramref name="limit"/> must be positive.
    /// </summary>
    public IReadOnlyList<LatticeNodeId> FindNodesByLabelProperty(
        string label, string property, LatticeValue value, int limit = 10)
    {
        lock (gate)
        {
            EnsureActive();
            ArgumentException.ThrowIfNullOrWhiteSpace(label);
            ArgumentException.ThrowIfNullOrWhiteSpace(property);
            ValidateNativeString(label, nameof(label));
            ValidateNativeString(property, nameof(property));
            using var nativeValue = new NativeValueBuilder(value);
            var root = nativeValue.Root;
            var error = NativeMethods.FindNodesByLabelProperty(
                handle.DangerousGetHandle(),
                label,
                property,
                in root,
                ValidatePositiveLimit(limit),
                out var ids,
                out var count);
            NativeError.ThrowIfFailed(error, "index/find-nodes");
            try
            {
                return NativeCollections.ReadNodeIds(ids, count);
            }
            finally
            {
                if (ids != nint.Zero)
                {
                    NativeMethods.FreeNodeIds(ids, count);
                }
            }
        }
    }

    /// <summary>
    /// Finds visible edge ids through an explicit type/property equality
    /// index. Fails when no such index exists rather than scanning.
    /// <paramref name="limit"/> must be positive.
    /// </summary>
    public IReadOnlyList<LatticeEdgeId> FindEdgesByTypeProperty(
        string edgeType, string property, LatticeValue value, int limit = 10)
    {
        lock (gate)
        {
            EnsureActive();
            ArgumentException.ThrowIfNullOrWhiteSpace(edgeType);
            ArgumentException.ThrowIfNullOrWhiteSpace(property);
            ValidateNativeString(edgeType, nameof(edgeType));
            ValidateNativeString(property, nameof(property));
            using var nativeValue = new NativeValueBuilder(value);
            var root = nativeValue.Root;
            var error = NativeMethods.FindEdgesByTypeProperty(
                handle.DangerousGetHandle(),
                edgeType,
                property,
                in root,
                ValidatePositiveLimit(limit),
                out var ids,
                out var count);
            NativeError.ThrowIfFailed(error, "index/find-edges");
            try
            {
                return NativeCollections.ReadEdgeIds(ids, count);
            }
            finally
            {
                if (ids != nint.Zero)
                {
                    NativeMethods.FreeEdgeIds(ids, count);
                }
            }
        }
    }

    /// <summary>Gets whether a node is visible in this transaction's snapshot.</summary>
    public bool NodeExists(LatticeNodeId nodeId)
    {
        lock (gate)
        {
            EnsureActive();
            var error = NativeMethods.NodeExists(handle.DangerousGetHandle(), nodeId.Value, out var exists);
            NativeError.ThrowIfFailed(error, "node/exists");
            return exists != 0;
        }
    }

    /// <summary>Creates a typed edge and returns its stable native identifier.</summary>
    public LatticeEdgeId CreateEdge(LatticeNodeId source, LatticeNodeId target, string edgeType)
    {
        lock (gate)
        {
            EnsureActive();
            EnsureWritable();
            ArgumentException.ThrowIfNullOrWhiteSpace(edgeType);
            ValidateNativeString(edgeType, nameof(edgeType));
            var error = NativeMethods.CreateEdge(
                handle.DangerousGetHandle(),
                source.Value,
                target.Value,
                edgeType,
                out var edgeId);
            NativeError.ThrowIfFailed(error, "edge/create");
            return new LatticeEdgeId(edgeId);
        }
    }

    /// <summary>Deletes the typed edge between two nodes.</summary>
    public void DeleteEdge(LatticeNodeId source, LatticeNodeId target, string edgeType)
    {
        lock (gate)
        {
            EnsureActive();
            EnsureWritable();
            ArgumentException.ThrowIfNullOrWhiteSpace(edgeType);
            ValidateNativeString(edgeType, nameof(edgeType));
            var error = NativeMethods.DeleteEdge(
                handle.DangerousGetHandle(),
                source.Value,
                target.Value,
                edgeType);
            NativeError.ThrowIfFailed(error, "edge/delete");
        }
    }

    /// <summary>Sets a detached property value on an edge.</summary>
    public void SetEdgeProperty(LatticeEdgeId edgeId, string key, LatticeValue value)
    {
        lock (gate)
        {
            EnsureActive();
            EnsureWritable();
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ValidateNativeString(key, nameof(key));
            using var nativeValue = new NativeValueBuilder(value);
            var root = nativeValue.Root;
            var error = NativeMethods.SetEdgeProperty(
                handle.DangerousGetHandle(),
                edgeId.Value,
                key,
                in root);
            NativeError.ThrowIfFailed(error, "edge/set-property");
        }
    }

    /// <summary>Gets a detached edge property value, or <see langword="false"/> when absent.</summary>
    public bool TryGetEdgeProperty(LatticeEdgeId edgeId, string key, out LatticeValue value)
    {
        lock (gate)
        {
            EnsureActive();
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ValidateNativeString(key, nameof(key));
            var error = NativeMethods.GetEdgeProperty(
                handle.DangerousGetHandle(),
                edgeId.Value,
                key,
                out var nativeValue);
            if (error == NativeErrorCode.NotFound)
            {
                value = LatticeValue.Null;
                return false;
            }

            NativeError.ThrowIfFailed(error, "edge/get-property");
            try
            {
                value = NativeValueConversion.ToManaged(nativeValue);
                return true;
            }
            finally
            {
                NativeMethods.FreeValue(ref nativeValue);
            }
        }
    }

    /// <summary>Gets a detached edge property value, throwing when the key is absent.</summary>
    public LatticeValue GetEdgeProperty(LatticeEdgeId edgeId, string key)
    {
        if (!TryGetEdgeProperty(edgeId, key, out var value))
        {
            throw new KeyNotFoundException($"Edge {edgeId.Value} has no property named '{key}'.");
        }

        return value;
    }

    /// <summary>Removes a property from an edge.</summary>
    public void RemoveEdgeProperty(LatticeEdgeId edgeId, string key)
    {
        lock (gate)
        {
            EnsureActive();
            EnsureWritable();
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ValidateNativeString(key, nameof(key));
            var error = NativeMethods.RemoveEdgeProperty(
                handle.DangerousGetHandle(),
                edgeId.Value,
                key);
            NativeError.ThrowIfFailed(error, "edge/remove-property");
        }
    }

    /// <summary>Gets the detached outgoing edges visible from a node.</summary>
    public IReadOnlyList<LatticeEdgeInfo> GetOutgoingEdges(LatticeNodeId nodeId) =>
        ReadEdges(outgoing: true, nodeId, null, 0);

    /// <summary>Gets the detached incoming edges visible to a node.</summary>
    public IReadOnlyList<LatticeEdgeInfo> GetIncomingEdges(LatticeNodeId nodeId) =>
        ReadEdges(outgoing: false, nodeId, null, 0);

    /// <summary>Gets detached outgoing edges of one type. Zero <paramref name="limit"/> means unlimited.</summary>
    public IReadOnlyList<LatticeEdgeInfo> GetOutgoingEdges(LatticeNodeId nodeId, string edgeType, int limit = 0) =>
        ReadEdges(outgoing: true, nodeId, edgeType, ValidateLimit(limit));

    /// <summary>Gets detached incoming edges of one type. Zero <paramref name="limit"/> means unlimited.</summary>
    public IReadOnlyList<LatticeEdgeInfo> GetIncomingEdges(LatticeNodeId nodeId, string edgeType, int limit = 0) =>
        ReadEdges(outgoing: false, nodeId, edgeType, ValidateLimit(limit));

    /// <summary>
    /// Creates multiple nodes with vectors in one call. On partial failure
    /// some nodes may exist; roll the transaction back and retry.
    /// </summary>
    public IReadOnlyList<LatticeNodeId> BatchInsertNodes(IReadOnlyList<LatticeNodeVector> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        if (nodes.Count == 0)
        {
            return [];
        }

        lock (gate)
        {
            EnsureActive();
            EnsureWritable();
            foreach (var node in nodes)
            {
                ArgumentNullException.ThrowIfNull(node);
                ValidateNativeString(node.Label, nameof(nodes));
                if (node.Vector.Length == 0)
                {
                    throw new ArgumentException("Batch vectors must contain at least one dimension.", nameof(nodes));
                }
            }

            var structSize = Marshal.SizeOf<NativeNodeWithVector>();
            var specs = Marshal.AllocHGlobal(checked(nodes.Count * structSize));
            var idsOut = Marshal.AllocHGlobal(checked(nodes.Count * sizeof(ulong)));
            var labels = new List<nint>(nodes.Count);
            var pinned = new List<GCHandle>(nodes.Count);
            try
            {
                for (var index = 0; index < nodes.Count; index++)
                {
                    var values = nodes[index].Vector.ToArray();
                    var handle = GCHandle.Alloc(values, GCHandleType.Pinned);
                    pinned.Add(handle);
                    var label = nint.Zero;
                    if (nodes[index].Label is not null)
                    {
                        var labelBytes = NativeText.Encode(nodes[index].Label!, nameof(nodes));
                        label = Marshal.AllocHGlobal(labelBytes.Length + 1);
                        Marshal.Copy(labelBytes, 0, label, labelBytes.Length);
                        Marshal.WriteByte(label + labelBytes.Length, 0);
                        labels.Add(label);
                    }

                    Marshal.StructureToPtr(
                        new NativeNodeWithVector
                        {
                            Label = label,
                            Vector = handle.AddrOfPinnedObject(),
                            Dimensions = checked((uint)values.Length)
                        },
                        specs + index * structSize,
                        false);
                }

                var error = NativeMethods.BatchInsert(
                    handle.DangerousGetHandle(),
                    specs,
                    checked((uint)nodes.Count),
                    idsOut,
                    out var created);
                NativeError.ThrowIfFailed(error, "node/batch-insert");
                var createdIds = new long[checked((int)created)];
                Marshal.Copy(idsOut, createdIds, 0, createdIds.Length);
                return createdIds.Select(id => new LatticeNodeId((ulong)id)).ToArray();
            }
            finally
            {
                foreach (var label in labels)
                {
                    Marshal.FreeHGlobal(label);
                }

                foreach (var handle in pinned)
                {
                    handle.Free();
                }

                Marshal.FreeHGlobal(specs);
                Marshal.FreeHGlobal(idsOut);
            }
        }
    }

    /// <summary>
    /// Searches vectors visible in this snapshot, returning up to
    /// <paramref name="count"/> hits ordered by increasing distance.
    /// </summary>
    public IReadOnlyList<LatticeVectorHit> VectorSearch(
        ReadOnlyMemory<float> query, int count, ushort efSearch = 0)
    {
        if (query.Length == 0)
        {
            throw new ArgumentException("A query vector must contain at least one dimension.", nameof(query));
        }

        if (count < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "The result count must be positive.");
        }

        var values = query.ToArray();
        var pinned = GCHandle.Alloc(values, GCHandleType.Pinned);
        try
        {
            lock (gate)
            {
                EnsureActive();
                var error = NativeMethods.VectorSearchTransaction(
                    handle.DangerousGetHandle(),
                    pinned.AddrOfPinnedObject(),
                    checked((uint)values.Length),
                    checked((uint)count),
                    efSearch,
                    out var result);
                NativeError.ThrowIfFailed(error, "vector/search");
                try
                {
                    return NativeCollections.ReadVectorHits(result);
                }
                finally
                {
                    NativeMethods.FreeVectorResult(result);
                }
            }
        }
        finally
        {
            pinned.Free();
        }
    }

    /// <summary>
    /// Searches one declared node index with BM25 scoring inside this
    /// snapshot. <paramref name="limit"/> must be positive.
    /// </summary>
    public IReadOnlyList<LatticeFtsHit> FtsSearch(string label, string property, string query, int limit)
    {
        lock (gate)
        {
            EnsureActive();
            ArgumentException.ThrowIfNullOrWhiteSpace(label);
            ArgumentException.ThrowIfNullOrWhiteSpace(property);
            ArgumentException.ThrowIfNullOrWhiteSpace(query);
            NativeText.Validate(query, nameof(query));
            if (limit <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(limit), "The result limit must be positive.");
            }

            var error = NativeMethods.FtsSearchTransaction(
                handle.DangerousGetHandle(),
                label,
                property,
                query,
                (nuint)NativeText.GetByteCount(query, nameof(query)),
                checked((uint)limit),
                out var result);
            NativeError.ThrowIfFailed(error, "fts/search");
            try
            {
                return NativeCollections.ReadFtsHits(result);
            }
            finally
            {
                NativeMethods.FreeFtsResult(result);
            }
        }
    }

    /// <summary>
    /// Searches one declared node index with typo tolerance inside this
    /// snapshot. Zero <paramref name="maxDistance"/> and
    /// <paramref name="minTermLength"/> select engine defaults.
    /// <paramref name="limit"/> must be positive.
    /// </summary>
    public IReadOnlyList<LatticeFtsHit> FtsSearchFuzzy(
        string label, string property, string query, int limit, int maxDistance = 0, int minTermLength = 0)
    {
        lock (gate)
        {
            EnsureActive();
            ArgumentException.ThrowIfNullOrWhiteSpace(label);
            ArgumentException.ThrowIfNullOrWhiteSpace(property);
            ArgumentException.ThrowIfNullOrWhiteSpace(query);
            NativeText.Validate(query, nameof(query));
            if (limit <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(limit), "The result limit must be positive.");
            }

            if (maxDistance < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxDistance));
            }

            if (minTermLength < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(minTermLength));
            }

            var error = NativeMethods.FtsSearchFuzzyTransaction(
                handle.DangerousGetHandle(),
                label,
                property,
                query,
                (nuint)NativeText.GetByteCount(query, nameof(query)),
                checked((uint)limit),
                checked((uint)maxDistance),
                checked((uint)minTermLength),
                out var result);
            NativeError.ThrowIfFailed(error, "fts/search-fuzzy");
            try
            {
                return NativeCollections.ReadFtsHits(result);
            }
            finally
            {
                NativeMethods.FreeFtsResult(result);
            }
        }
    }

    /// <summary>Commits the transaction. Recoverable failures leave it available for retry or rollback.</summary>
    public void Commit()
    {
        lock (gate)
        {
            EnsureActive();
            var error = NativeMethods.Commit(handle.DangerousGetHandle());
            if (error is NativeErrorCode.Ok or NativeErrorCode.TransactionAborted)
            {
                Complete();
            }

            NativeError.ThrowIfFailed(error, "transaction/commit");
        }
    }

    /// <summary>Rolls back the transaction and consumes it even when native rollback reports failure.</summary>
    public void Rollback()
    {
        lock (gate)
        {
            EnsureActive();
            var error = NativeMethods.Rollback(handle.DangerousGetHandle());
            Complete();
            NativeError.ThrowIfFailed(error, "transaction/rollback");
        }
    }

    /// <summary>
    /// Rolls back an unfinished transaction on scope exit. Rollback failures are
    /// intentionally ignored so a callback or using-body exception is not masked.
    /// Use <see cref="Rollback"/> when the rollback result must be checked.
    /// </summary>
    public void Dispose()
    {
        lock (gate)
        {
            if (completed)
            {
                return;
            }

            try
            {
                _ = NativeMethods.Rollback(handle.DangerousGetHandle());
            }
            catch
            {
                // Scope cleanup must not mask an exception already escaping a
                // using body. Completion still releases the managed ownership.
            }
            finally
            {
                Complete();
            }
        }
    }

    private static void ValidateNativeString(string? value, string parameterName)
    {
        if (value is null)
        {
            return;
        }

        NativeText.Validate(value, parameterName);
        if (value.Contains('\0', StringComparison.Ordinal))
        {
            throw new ArgumentException("Value cannot contain a null character.", parameterName);
        }
    }

    private static void ValidateLabel(string? label, string parameterName)
    {
        ValidateNativeString(label, parameterName);
        if (label is not null && label.Contains(',', StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Labels cannot contain commas because the pinned native label getter returns a comma-separated string.",
                parameterName);
        }
    }

    internal static IReadOnlyList<string> DecodeLabels(nint labelsPointer)
    {
        if (labelsPointer == nint.Zero)
        {
            throw new InvalidDataException("Native node label operation returned a null label string.");
        }

        var labels = NativeText.DecodeNullTerminated(labelsPointer)
            ?? throw new InvalidDataException("Native node label operation returned null text.");
        if (labels.Length == 0)
        {
            return Array.Empty<string>();
        }

        // lattice_node_get_labels has no count/escaping mechanism and joins
        // labels with commas. Validate labels at every write boundary so this
        // split cannot silently change label identity.
        return Array.AsReadOnly(labels.Split(',', StringSplitOptions.None));
    }

    private void EnsureActive()
    {
        ObjectDisposedException.ThrowIf(completed, this);
    }

    private void EnsureWritable()
    {
        if (Mode != LatticeTransactionMode.ReadWrite)
        {
            throw new InvalidOperationException("The operation requires a read-write transaction.");
        }
    }

    private void Complete()
    {
        handle.MarkConsumed();
        handle.Dispose();
        completed = true;
        database.ReleaseChild();
    }

    private static uint ValidateLimit(int limit)
    {
        if (limit < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "The traversal limit cannot be negative.");
        }

        return checked((uint)limit);
    }

    private static uint ValidatePositiveLimit(int limit)
    {
        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "The find limit must be positive.");
        }

        return checked((uint)limit);
    }

    private IReadOnlyList<LatticeEdgeInfo> ReadEdges(bool outgoing, LatticeNodeId nodeId, string? edgeType, uint limit)
    {
        lock (gate)
        {
            EnsureActive();
            if (edgeType is not null)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(edgeType);
                ValidateNativeString(edgeType, nameof(edgeType));
            }

            var handleValue = handle.DangerousGetHandle();
            nint result;
            NativeErrorCode error;
            if (edgeType is null)
            {
                error = outgoing
                    ? NativeMethods.GetOutgoingEdges(handleValue, nodeId.Value, out result)
                    : NativeMethods.GetIncomingEdges(handleValue, nodeId.Value, out result);
            }
            else
            {
                error = outgoing
                    ? NativeMethods.GetOutgoingEdgesByType(handleValue, nodeId.Value, edgeType, limit, out result)
                    : NativeMethods.GetIncomingEdgesByType(handleValue, nodeId.Value, edgeType, limit, out result);
            }

            NativeError.ThrowIfFailed(error, "edge/traverse");
            try
            {
                var count = NativeMethods.EdgeResultCount(result);
                var edges = new List<LatticeEdgeInfo>();
                for (var index = 0u; index < count; index++)
                {
                    var idError = NativeMethods.EdgeResultGetId(result, index, out var edgeId);
                    NativeError.ThrowIfFailed(idError, "edge/read");
                    var getError = NativeMethods.EdgeResultGet(
                        result,
                        index,
                        out var source,
                        out var target,
                        out var typePointer,
                        out var typeLength);
                    NativeError.ThrowIfFailed(getError, "edge/read");
                    edges.Add(new LatticeEdgeInfo(
                        new LatticeEdgeId(edgeId),
                        new LatticeNodeId(source),
                        new LatticeNodeId(target),
                        NativeText.Decode(typePointer, checked((int)typeLength), "edge type")));
                }

                return edges.AsReadOnly();
            }
            finally
            {
                NativeMethods.FreeEdgeResult(result);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeNodeWithVector
    {
        public nint Label;
        public nint Vector;
        public uint Dimensions;
    }

    internal T WithActiveHandle<T>(Func<nint, T> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        lock (gate)
        {
            EnsureActive();
            return action(handle.DangerousGetHandle());
        }
    }
}

/// <summary>A stable native node identifier.</summary>
public readonly record struct LatticeNodeId(ulong Value);

/// <summary>A stable native edge identifier.</summary>
public readonly record struct LatticeEdgeId(ulong Value);

/// <summary>A label plus vector for one batch-inserted node.</summary>
public sealed record LatticeNodeVector(string? Label, ReadOnlyMemory<float> Vector);

/// <summary>A detached vector-search hit.</summary>
public sealed record LatticeVectorHit(LatticeNodeId NodeId, float Distance);

/// <summary>A detached full-text search hit with its BM25 score.</summary>
public sealed record LatticeFtsHit(LatticeNodeId NodeId, float Score);

/// <summary>A detached edge observed by traversal.</summary>
public sealed record LatticeEdgeInfo(
    LatticeEdgeId Id,
    LatticeNodeId Source,
    LatticeNodeId Target,
    string Type);
