using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using LatticeDbSharp.Interop;

namespace LatticeDbSharp;

/// <summary>A reusable prepared Cypher query owned by a database.</summary>
public sealed class LatticeQuery : IDisposable
{
    private readonly object gate = new();
    private readonly LatticeDatabase database;
    private readonly SafeLatticeQueryHandle handle;
    private int activeResults;
    private bool disposed;

    internal LatticeQuery(LatticeDatabase database, SafeLatticeQueryHandle handle)
    {
        this.database = database;
        this.handle = handle;
    }

    /// <summary>
    /// Gets whether the native planner classifies this prepared query as
    /// potentially writing. This is advisory metadata, not authorization.
    /// </summary>
    public bool MayWrite
    {
        get
        {
            lock (gate)
            {
                EnsureActive();
                return NativeMethods.QueryWrites(handle.DangerousGetHandle()) != 0;
            }
        }
    }

    /// <summary>Binds or replaces a named parameter. The name excludes the Cypher '$' prefix.</summary>
    public void Bind(string name, LatticeValue value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Contains('\0', StringComparison.Ordinal))
        {
            throw new ArgumentException("Parameter name cannot contain a null character.", nameof(name));
        }

        NativeText.Validate(name, nameof(name));

        using var nativeValue = new NativeValueBuilder(value);
        lock (gate)
        {
            EnsureActive();
            var root = nativeValue.Root;
            var error = NativeMethods.BindQueryParameter(handle.DangerousGetHandle(), name, in root);
            NativeError.ThrowIfFailed(error, "query/bind");
        }
    }

    /// <summary>Executes this query inside an active transaction.</summary>
    public LatticeQueryResult Execute(LatticeTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        if (!ReferenceEquals(database, transaction.Database))
        {
            throw new ArgumentException("The transaction belongs to a different database.", nameof(transaction));
        }

        lock (gate)
        {
            EnsureActive();
            var nativeResult = transaction.WithActiveHandle(transactionHandle =>
            {
                var error = NativeMethods.ExecuteQuery(
                    handle.DangerousGetHandle(),
                    transactionHandle,
                    out var result);
                if (error != NativeErrorCode.Ok)
                {
                    if (result != nint.Zero)
                    {
                        NativeMethods.FreeResult(result);
                    }

                    throw CreateQueryException(error);
                }

                if (result == nint.Zero)
                {
                    throw new LatticeException("query/execute", NativeErrorCode.Error);
                }

                return result;
            });

            IReadOnlyList<string> columns;
            try
            {
                columns = ReadColumns(nativeResult);
            }
            catch
            {
                NativeMethods.FreeResult(nativeResult);
                throw;
            }

            SafeLatticeResultHandle resultHandle;
            try
            {
                resultHandle = new SafeLatticeResultHandle(
                    nativeResult,
                    handle,
                    ReleaseResultFromHandle);
            }
            catch
            {
                NativeMethods.FreeResult(nativeResult);
                throw;
            }

            activeResults++;
            try
            {
                return new LatticeQueryResult(this, resultHandle, columns);
            }
            catch
            {
                resultHandle.Dispose();
                throw;
            }
        }
    }

    /// <summary>Executes the query and eagerly materializes detached rows.</summary>
    public IReadOnlyList<LatticeRow> ExecuteAll(LatticeTransaction transaction)
    {
        using var result = Execute(transaction);
        return result.ReadAll();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            if (activeResults != 0)
            {
                throw new InvalidOperationException("Dispose the query result before disposing its query.");
            }

            handle.Dispose();
            disposed = true;
        }
    }

    internal void ReleaseResultFromHandle()
    {
        lock (gate)
        {
            if (activeResults > 0)
            {
                activeResults--;
            }
        }
    }

    private IReadOnlyList<string> ReadColumns(nint result)
    {
        var count = checked((int)NativeMethods.ResultColumnCount(result));
        var columns = new string[count];
        for (var index = 0; index < count; index++)
        {
            var pointer = NativeMethods.ResultColumnName(result, (uint)index);
            columns[index] = NativeText.DecodeNullTerminated(pointer)
                ?? throw new InvalidDataException("Native query result returned a null column name.");
        }

        return new ReadOnlyCollection<string>(columns);
    }

    private LatticeQueryException CreateQueryException(NativeErrorCode error)
    {
        var stage = (LatticeQueryStage)NativeMethods.GetQueryErrorStage(handle.DangerousGetHandle());
        var message = NativeText.DecodeNullTerminated(
            NativeMethods.GetQueryErrorMessage(handle.DangerousGetHandle()))
            ?? "Native query execution failed.";
        var code = NativeText.DecodeNullTerminated(NativeMethods.GetQueryErrorCode(handle.DangerousGetHandle()));
        var hasLocation = NativeMethods.QueryErrorHasLocation(handle.DangerousGetHandle()) != 0;
        int? line = hasLocation ? checked((int)NativeMethods.GetQueryErrorLine(handle.DangerousGetHandle())) : null;
        int? column = hasLocation ? checked((int)NativeMethods.GetQueryErrorColumn(handle.DangerousGetHandle())) : null;
        int? length = hasLocation ? checked((int)NativeMethods.GetQueryErrorLength(handle.DangerousGetHandle())) : null;
        return new LatticeQueryException(
            "query/execute",
            error,
            $"LatticeDB query failed at {stage}: {message}",
            code,
            stage,
            line,
            column,
            length);
    }

    private void EnsureActive()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }
}
