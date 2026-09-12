using System.Runtime.InteropServices;
using LatticeDbSharp.Interop;

namespace LatticeDbSharp;

/// <summary>An owned connection to an embedded LatticeDB database.</summary>
public sealed class LatticeDatabase : IDisposable
{
    private readonly object gate = new();
    private readonly SafeLatticeDatabaseHandle handle;
    private int activeTransactions;
    private int activeQueries;
    private bool closeStarted;
    private bool closed;

    private LatticeDatabase(string path, SafeLatticeDatabaseHandle handle)
    {
        Path = path;
        this.handle = handle;
    }

    /// <summary>The normalized database path, or <c>:memory:</c>.</summary>
    public string Path { get; }

    /// <summary>Gets whether this managed owner has completed native close.</summary>
    public bool IsClosed
    {
        get
        {
            lock (gate)
            {
                return closed;
            }
        }
    }

    /// <summary>Opens a file-backed database.</summary>
    public static LatticeDatabase Open(string path, LatticeDatabaseOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (path.Contains('\0', StringComparison.Ordinal))
        {
            throw new ArgumentException("Database path cannot contain a null character.", nameof(path));
        }

        NativeText.Validate(path, nameof(path));

        var normalizedPath = string.Equals(path, ":memory:", StringComparison.Ordinal)
            ? path
            : System.IO.Path.GetFullPath(path);
        var effectiveOptions = options ?? new LatticeDatabaseOptions();
        var nativeOptions = effectiveOptions.ToNative();

        // Validate the loaded ABI before passing versioned structs to native code.
        // This also turns missing/mismatched libraries into the documented native
        // boundary exceptions instead of allowing an arbitrary export-compatible
        // library to interpret the options.
        _ = LatticeNative.Version;
        NativeErrorCode error;
        nint nativeHandle;
        try
        {
            error = NativeMethods.OpenV4(normalizedPath, in nativeOptions, out nativeHandle);
        }
        catch (Exception exception) when (
            exception is DllNotFoundException or
            BadImageFormatException or
            EntryPointNotFoundException)
        {
            throw new LatticeNativeLoadException(
                $"Unable to open LatticeDB {LatticeNative.PinnedVersion}. " +
                $"{NativeLibraryResolver.DescribeSearch()}.",
                exception);
        }
        NativeError.ThrowIfFailed(error, "database/open");
        if (nativeHandle == nint.Zero)
        {
            throw new LatticeException("database/open", NativeErrorCode.Error);
        }

        return new LatticeDatabase(normalizedPath, new SafeLatticeDatabaseHandle(nativeHandle));
    }

    /// <summary>Opens an isolated in-memory database.</summary>
    public static LatticeDatabase OpenMemory(LatticeDatabaseOptions? options = null)
    {
        return Open(":memory:", options);
    }

    /// <summary>Returns every node id currently carrying a label. Unknown labels yield an empty list.</summary>
    public IReadOnlyList<LatticeNodeId> GetNodesByLabel(string label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        NativeText.Validate(label, nameof(label));
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(closed, this);
            if (closeStarted)
            {
                throw new InvalidOperationException("Database close has started.");
            }

            var error = NativeMethods.GetNodesByLabel(
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

    /// <summary>
    /// Captures the whole database as bytes, folding pending writes first.
    /// Fails while a transaction is open. Write the bytes anywhere; they open
    /// with <see cref="Deserialize(byte[], LatticeDatabaseOptions?)"/>.
    /// </summary>
    public byte[] Serialize()
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(closed, this);
            if (closeStarted)
            {
                throw new InvalidOperationException("Database close has started.");
            }

            var error = NativeMethods.SerializeDatabase(handle.DangerousGetHandle(), out var bytes, out var length);
            NativeError.ThrowIfFailed(error, "database/serialize");
            try
            {
                if (bytes == nint.Zero)
                {
                    throw new LatticeException("database/serialize", NativeErrorCode.Error);
                }

                var count = checked((int)length);
                var buffer = new byte[count];
                Marshal.Copy(bytes, buffer, 0, count);
                return buffer;
            }
            finally
            {
                if (bytes != nint.Zero)
                {
                    NativeMethods.FreeBytes(bytes, length);
                }
            }
        }
    }

    /// <summary>
    /// Opens an independent database from bytes produced by <see cref="Serialize"/>.
    /// The bytes are copied and may be released as soon as this returns.
    /// </summary>
    public static LatticeDatabase Deserialize(byte[] data, LatticeDatabaseOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length == 0)
        {
            throw new ArgumentException("Serialized database bytes cannot be empty.", nameof(data));
        }

        var effectiveOptions = options ?? new LatticeDatabaseOptions();
        var nativeOptions = effectiveOptions.ToNative();
        _ = LatticeNative.Version;
        NativeErrorCode error;
        nint nativeHandle;
        unsafe
        {
            fixed (byte* pointer = data)
            {
                try
                {
                    error = NativeMethods.DeserializeDatabase((nint)pointer, (nuint)data.Length, in nativeOptions, out nativeHandle);
                }
                catch (Exception exception) when (
                    exception is DllNotFoundException or
                    BadImageFormatException or
                    EntryPointNotFoundException)
                {
                    throw new LatticeNativeLoadException(
                        $"Unable to open LatticeDB {LatticeNative.PinnedVersion}. " +
                        $"{NativeLibraryResolver.DescribeSearch()}.",
                        exception);
                }
            }
        }
        NativeError.ThrowIfFailed(error, "database/deserialize");
        if (nativeHandle == nint.Zero)
        {
            throw new LatticeException("database/deserialize", NativeErrorCode.Error);
        }

        return new LatticeDatabase("<deserialized>", new SafeLatticeDatabaseHandle(nativeHandle));
    }

    /// <summary>
    /// Creates an equality index for a node label/property pair, scanning
    /// existing matching nodes. Fails while a write transaction is active.
    /// </summary>
    public void CreateNodePropertyIndex(string label, string property)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentException.ThrowIfNullOrWhiteSpace(property);
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(closed, this);
            if (closeStarted)
            {
                throw new InvalidOperationException("Database close has started.");
            }

            var error = NativeMethods.CreateNodePropertyIndex(handle.DangerousGetHandle(), label, property);
            NativeError.ThrowIfFailed(error, "index/create-node-property");
        }
    }

    /// <summary>Drops an equality index for a node label/property pair.</summary>
    public void DropNodePropertyIndex(string label, string property)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentException.ThrowIfNullOrWhiteSpace(property);
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(closed, this);
            if (closeStarted)
            {
                throw new InvalidOperationException("Database close has started.");
            }

            var error = NativeMethods.DropNodePropertyIndex(handle.DangerousGetHandle(), label, property);
            NativeError.ThrowIfFailed(error, "index/drop-node-property");
        }
    }

    /// <summary>
    /// Creates an equality index for an edge type/property pair, scanning
    /// existing matching edges. Fails while a write transaction is active.
    /// </summary>
    public void CreateEdgePropertyIndex(string edgeType, string property)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(edgeType);
        ArgumentException.ThrowIfNullOrWhiteSpace(property);
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(closed, this);
            if (closeStarted)
            {
                throw new InvalidOperationException("Database close has started.");
            }

            var error = NativeMethods.CreateEdgePropertyIndex(handle.DangerousGetHandle(), edgeType, property);
            NativeError.ThrowIfFailed(error, "index/create-edge-property");
        }
    }

    /// <summary>Drops an equality index for an edge type/property pair.</summary>
    public void DropEdgePropertyIndex(string edgeType, string property)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(edgeType);
        ArgumentException.ThrowIfNullOrWhiteSpace(property);
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(closed, this);
            if (closeStarted)
            {
                throw new InvalidOperationException("Database close has started.");
            }

            var error = NativeMethods.DropEdgePropertyIndex(handle.DangerousGetHandle(), edgeType, property);
            NativeError.ThrowIfFailed(error, "index/drop-edge-property");
        }
    }

    /// <summary>
    /// Searches vectors across the database, returning up to
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
                ObjectDisposedException.ThrowIf(closed, this);
                if (closeStarted)
                {
                    throw new InvalidOperationException("Database close has started.");
                }

                var error = NativeMethods.VectorSearch(
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

    /// <summary>Creates a full-text index over one node label/property pair of string properties.</summary>
    public void CreateNodeFtsIndex(string label, string property)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentException.ThrowIfNullOrWhiteSpace(property);
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(closed, this);
            if (closeStarted)
            {
                throw new InvalidOperationException("Database close has started.");
            }

            var error = NativeMethods.CreateNodeFtsIndex(handle.DangerousGetHandle(), label, property);
            NativeError.ThrowIfFailed(error, "index/create-node-fts");
        }
    }

    /// <summary>Drops a full-text index over one node label/property pair.</summary>
    public void DropNodeFtsIndex(string label, string property)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentException.ThrowIfNullOrWhiteSpace(property);
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(closed, this);
            if (closeStarted)
            {
                throw new InvalidOperationException("Database close has started.");
            }

            var error = NativeMethods.DropNodeFtsIndex(handle.DangerousGetHandle(), label, property);
            NativeError.ThrowIfFailed(error, "index/drop-node-fts");
        }
    }

    /// <summary>Gets whether a node full-text index exists.</summary>
    public bool NodeFtsIndexExists(string label, string property)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentException.ThrowIfNullOrWhiteSpace(property);
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(closed, this);
            if (closeStarted)
            {
                throw new InvalidOperationException("Database close has started.");
            }

            var error = NativeMethods.NodeFtsIndexExists(handle.DangerousGetHandle(), label, property, out var exists);
            NativeError.ThrowIfFailed(error, "index/check-node-fts");
            return exists != 0;
        }
    }

    /// <summary>Creates a full-text index over one edge type/property pair of string properties.</summary>
    public void CreateEdgeFtsIndex(string edgeType, string property)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(edgeType);
        ArgumentException.ThrowIfNullOrWhiteSpace(property);
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(closed, this);
            if (closeStarted)
            {
                throw new InvalidOperationException("Database close has started.");
            }

            var error = NativeMethods.CreateEdgeFtsIndex(handle.DangerousGetHandle(), edgeType, property);
            NativeError.ThrowIfFailed(error, "index/create-edge-fts");
        }
    }

    /// <summary>Drops a full-text index over one edge type/property pair.</summary>
    public void DropEdgeFtsIndex(string edgeType, string property)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(edgeType);
        ArgumentException.ThrowIfNullOrWhiteSpace(property);
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(closed, this);
            if (closeStarted)
            {
                throw new InvalidOperationException("Database close has started.");
            }

            var error = NativeMethods.DropEdgeFtsIndex(handle.DangerousGetHandle(), edgeType, property);
            NativeError.ThrowIfFailed(error, "index/drop-edge-fts");
        }
    }

    /// <summary>Gets whether an edge full-text index exists.</summary>
    public bool EdgeFtsIndexExists(string edgeType, string property)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(edgeType);
        ArgumentException.ThrowIfNullOrWhiteSpace(property);
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(closed, this);
            if (closeStarted)
            {
                throw new InvalidOperationException("Database close has started.");
            }

            var error = NativeMethods.EdgeFtsIndexExists(handle.DangerousGetHandle(), edgeType, property, out var exists);
            NativeError.ThrowIfFailed(error, "index/check-edge-fts");
            return exists != 0;
        }
    }

    /// <summary>
    /// Searches one declared node index with BM25 scoring. Searching without a
    /// declared index fails instead of returning no rows.
    /// <paramref name="limit"/> must be positive.
    /// </summary>
    public IReadOnlyList<LatticeFtsHit> FtsSearch(string label, string property, string query, int limit)
    {
        ValidateFtsArguments(label, property, query, limit);
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(closed, this);
            if (closeStarted)
            {
                throw new InvalidOperationException("Database close has started.");
            }

            var error = NativeMethods.FtsSearch(
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
    /// Searches one declared node index with typo tolerance. Zero
    /// <paramref name="maxDistance"/> and <paramref name="minTermLength"/>
    /// select engine defaults. <paramref name="limit"/> must be positive.
    /// </summary>
    public IReadOnlyList<LatticeFtsHit> FtsSearchFuzzy(
        string label, string property, string query, int limit, int maxDistance = 0, int minTermLength = 0)
    {
        ValidateFtsArguments(label, property, query, limit);
        if (maxDistance < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDistance));
        }

        if (minTermLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minTermLength));
        }

        lock (gate)
        {
            ObjectDisposedException.ThrowIf(closed, this);
            if (closeStarted)
            {
                throw new InvalidOperationException("Database close has started.");
            }

            var error = NativeMethods.FtsSearchFuzzy(
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

    private static void ValidateFtsArguments(string label, string property, string query, int limit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentException.ThrowIfNullOrWhiteSpace(property);
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        NativeText.Validate(query, nameof(query));
        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "The result limit must be positive.");
        }
    }

    /// <summary>Begins an explicit read-only transaction.</summary>
    public LatticeTransaction BeginReadTransaction()
    {
        return BeginTransaction(LatticeTransactionMode.ReadOnly);
    }

    /// <summary>Begins an explicit read-write transaction.</summary>
    public LatticeTransaction BeginWriteTransaction()
    {
        return BeginTransaction(LatticeTransactionMode.ReadWrite);
    }

    /// <summary>Runs a read-only transaction and commits it when the callback succeeds.</summary>
    public void ExecuteRead(Action<LatticeTransaction> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        ExecuteTransaction(LatticeTransactionMode.ReadOnly, transaction =>
        {
            action(transaction);
            return 0;
        });
    }

    /// <summary>Runs a read-only transaction and returns the callback's detached result.</summary>
    public TResult ExecuteRead<TResult>(Func<LatticeTransaction, TResult> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return ExecuteTransaction(LatticeTransactionMode.ReadOnly, action);
    }

    /// <summary>Runs a read-write transaction and commits it when the callback succeeds.</summary>
    public void ExecuteWrite(Action<LatticeTransaction> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        ExecuteTransaction(LatticeTransactionMode.ReadWrite, transaction =>
        {
            action(transaction);
            return 0;
        });
    }

    /// <summary>Runs a read-write transaction and returns the callback's detached result.</summary>
    public TResult ExecuteWrite<TResult>(Func<LatticeTransaction, TResult> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return ExecuteTransaction(LatticeTransactionMode.ReadWrite, action);
    }

    /// <summary>Prepares a parameterized Cypher query for this database.</summary>
    public LatticeQuery Prepare(string cypher)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cypher);
        if (cypher.Contains('\0', StringComparison.Ordinal))
        {
            throw new ArgumentException("Cypher cannot contain a null character.", nameof(cypher));
        }

        NativeText.Validate(cypher, nameof(cypher));

        lock (gate)
        {
            ObjectDisposedException.ThrowIf(closed, this);
            if (closeStarted)
            {
                throw new InvalidOperationException("Database close has started.");
            }

            var error = NativeMethods.PrepareQuery(handle.DangerousGetHandle(), cypher, out var nativeQuery);
            NativeError.ThrowIfFailed(error, "query/prepare");
            if (nativeQuery == nint.Zero)
            {
                throw new LatticeException("query/prepare", NativeErrorCode.Error);
            }

            SafeLatticeQueryHandle queryHandle;
            try
            {
                queryHandle = new SafeLatticeQueryHandle(nativeQuery, handle, ReleaseQueryFromFinalizer);
            }
            catch
            {
                NativeMethods.FreeQuery(nativeQuery);
                throw;
            }

            activeQueries++;
            try
            {
                return new LatticeQuery(this, queryHandle);
            }
            catch
            {
                queryHandle.Dispose();
                throw;
            }
        }
    }

    /// <summary>Closes the database after all child resources have completed.</summary>
    public void Close()
    {
        lock (gate)
        {
            if (closed)
            {
                return;
            }

            if (closeStarted)
            {
                throw new InvalidOperationException("Database close is already in progress.");
            }

            if (activeTransactions != 0 || activeQueries != 0)
            {
                var details = new List<string>();
                if (activeTransactions != 0)
                {
                    details.Add($"{activeTransactions} transaction(s)");
                }

                if (activeQueries != 0)
                {
                    details.Add(activeQueries == 1
                        ? "1 prepared query"
                        : $"{activeQueries} prepared queries");
                }

                throw new InvalidOperationException(
                    $"Database cannot close while {string.Join(" and ", details)} remain active.");
            }

            closeStarted = true;
            var error = handle.CloseChecked();
            if (error == NativeErrorCode.InvalidArgument)
            {
                closeStarted = false;
                NativeError.ThrowIfFailed(error, "database/close");
            }

            closed = true;
            handle.Dispose();
            NativeError.ThrowIfFailed(error, "database/close");
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Close();
    }

    internal void ReleaseChild()
    {
        lock (gate)
        {
            if (activeTransactions <= 0)
            {
                throw new InvalidOperationException("Database child ownership is unbalanced.");
            }

            activeTransactions--;
        }
    }

    internal void ReleaseQueryChild()
    {
        lock (gate)
        {
            if (activeQueries <= 0)
            {
                throw new InvalidOperationException("Database query ownership is unbalanced.");
            }

            activeQueries--;
        }
    }

    private LatticeTransaction BeginTransaction(LatticeTransactionMode mode)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(closed, this);
            if (closeStarted)
            {
                throw new InvalidOperationException("Database close has started.");
            }

            var nativeMode = mode == LatticeTransactionMode.ReadOnly
                ? NativeTransactionMode.ReadOnly
                : NativeTransactionMode.ReadWrite;
            var error = NativeMethods.Begin(handle.DangerousGetHandle(), nativeMode, out var nativeTransaction);
            NativeError.ThrowIfFailed(error, "transaction/begin");
            if (nativeTransaction == nint.Zero)
            {
                throw new LatticeException("transaction/begin", NativeErrorCode.Error);
            }

            SafeLatticeTransactionHandle transactionHandle;
            try
            {
                transactionHandle = new SafeLatticeTransactionHandle(
                    nativeTransaction,
                    handle,
                    ReleaseChildFromFinalizer);
            }
            catch
            {
                // The native transaction was created, but its managed owner
                // could not be constructed. Release it immediately rather
                // than waiting for a SafeHandle that was never published.
                _ = NativeMethods.Rollback(nativeTransaction);
                throw;
            }

            activeTransactions++;
            try
            {
                return new LatticeTransaction(this, transactionHandle, mode);
            }
            catch
            {
                // Dispose while the child count is registered so the handle's
                // owner-release callback balances the count on construction
                // failure as well.
                transactionHandle.Dispose();
                throw;
            }
        }
    }

    private TResult ExecuteTransaction<TResult>(
        LatticeTransactionMode mode,
        Func<LatticeTransaction, TResult> action)
    {
        var transaction = BeginTransaction(mode);
        try
        {
            try
            {
                var result = action(transaction);
                if (result is LatticeQueryResult queryResult)
                {
                    queryResult.Dispose();
                    throw new InvalidOperationException(
                        "A scoped transaction callback must return detached data; " +
                        "materialize query results with LatticeQuery.ExecuteAll first.");
                }

                if (result is LatticeTransaction)
                {
                    throw new InvalidOperationException(
                        "A scoped transaction callback cannot return its transaction owner.");
                }

                transaction.Commit();
                return result;
            }
            catch (Exception primaryException)
            {
                if (transaction.IsCompleted)
                {
                    throw;
                }

                try
                {
                    transaction.Rollback();
                }
                catch (Exception rollbackException)
                {
                    throw new AggregateException(
                        "The transaction operation failed and rollback also failed.",
                        primaryException,
                        rollbackException);
                }

                throw;
            }
        }
        finally
        {
            transaction.Dispose();
        }
    }

    private void ReleaseChildFromFinalizer()
    {
        lock (gate)
        {
            if (activeTransactions > 0)
            {
                activeTransactions--;
            }
        }
    }

    private void ReleaseQueryFromFinalizer()
    {
        lock (gate)
        {
            if (activeQueries > 0)
            {
                activeQueries--;
            }
        }
    }
}
