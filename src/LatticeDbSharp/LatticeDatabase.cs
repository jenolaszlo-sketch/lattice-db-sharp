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
