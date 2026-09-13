using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using LatticeDbSharp.Interop;

namespace LatticeDbSharp;

/// <summary>A reusable prepared Cypher query owned by a database.</summary>
/// <remarks>
/// Individual operations are synchronized. Callers sharing a query should
/// serialize a binding and execution sequence, or use the parameter-dictionary
/// overload of <see cref="Execute(LatticeTransaction, IReadOnlyDictionary{string, LatticeValue})"/>.
/// </remarks>
public sealed class LatticeQuery : IDisposable
{
    private readonly object gate = new();
    private readonly LatticeDatabase database;
    private readonly SafeLatticeQueryHandle handle;
    private HashSet<string>? dictionaryParameterNames;
    private bool hasManualBindings;
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
        ValidateParameterName(name);
        lock (gate)
        {
            EnsureActive();
            EnsureManualBindingMode();
            BindCore(name, value);
            hasManualBindings = true;
        }
    }

    /// <summary>Binds a vector parameter for similarity queries.</summary>
    public void BindVector(string name, ReadOnlyMemory<float> vector)
    {
        ValidateParameterName(name);
        if (vector.Length == 0)
        {
            throw new ArgumentException("A vector must contain at least one dimension.", nameof(vector));
        }

        var values = vector.ToArray();
        var pinned = GCHandle.Alloc(values, GCHandleType.Pinned);
        try
        {
            lock (gate)
            {
                EnsureActive();
                EnsureManualBindingMode();
                var error = NativeMethods.BindQueryVector(
                    handle.DangerousGetHandle(),
                    name,
                    pinned.AddrOfPinnedObject(),
                    checked((uint)values.Length));
                NativeError.ThrowIfFailed(error, "query/bind-vector");
                hasManualBindings = true;
            }
        }
        finally
        {
            pinned.Free();
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

    /// <summary>
    /// Binds the supplied parameters and executes the query while holding one
    /// synchronization scope. Parameter names exclude the Cypher '$' prefix.
    /// </summary>
    public LatticeQueryResult Execute(
        LatticeTransaction transaction,
        IReadOnlyDictionary<string, LatticeValue> parameters)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(parameters);
        if (!ReferenceEquals(database, transaction.Database))
        {
            throw new ArgumentException("The transaction belongs to a different database.", nameof(transaction));
        }

        var bindings = parameters.ToArray();
        foreach (var binding in bindings)
        {
            ValidateParameterName(binding.Key);
        }

        lock (gate)
        {
            EnsureActive();
            if (hasManualBindings)
            {
                throw new InvalidOperationException(
                    "Parameter-dictionary execution cannot follow individual Bind calls because native bindings cannot be cleared.");
            }

            var names = bindings.Select(binding => binding.Key).ToHashSet(StringComparer.Ordinal);
            if (dictionaryParameterNames is not null && !dictionaryParameterNames.SetEquals(names))
            {
                throw new InvalidOperationException(
                    "Every parameter-dictionary execution on a prepared query must use the same parameter names because native bindings cannot be cleared.");
            }

            foreach (var binding in bindings)
            {
                BindCore(binding.Key, binding.Value);
            }

            dictionaryParameterNames ??= names;

            return Execute(transaction);
        }
    }

    /// <summary>Executes the query and eagerly materializes detached rows.</summary>
    public IReadOnlyList<LatticeRow> ExecuteAll(LatticeTransaction transaction)
    {
        using var result = Execute(transaction);
        return result.ReadAll();
    }

    /// <summary>Binds one complete parameter set and eagerly materializes all detached rows.</summary>
    public IReadOnlyList<LatticeRow> ExecuteAll(
        LatticeTransaction transaction,
        IReadOnlyDictionary<string, LatticeValue> parameters)
    {
        using var result = Execute(transaction, parameters);
        return result.ReadAll();
    }

    /// <summary>Returns the only row, throwing when the query returns zero or multiple rows.</summary>
    public LatticeRow ExecuteSingle(LatticeTransaction transaction) => ReadSingle(Execute(transaction));

    /// <summary>Binds one complete parameter set and returns the only row.</summary>
    public LatticeRow ExecuteSingle(
        LatticeTransaction transaction,
        IReadOnlyDictionary<string, LatticeValue> parameters) => ReadSingle(Execute(transaction, parameters));

    /// <summary>Returns the only column of the only row, throwing for any other cardinality.</summary>
    public LatticeValue ExecuteScalar(LatticeTransaction transaction) => ReadScalar(ExecuteSingle(transaction));

    /// <summary>Binds one complete parameter set and returns the only column of the only row.</summary>
    public LatticeValue ExecuteScalar(
        LatticeTransaction transaction,
        IReadOnlyDictionary<string, LatticeValue> parameters) => ReadScalar(ExecuteSingle(transaction, parameters));

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

    private void BindCore(string name, LatticeValue value)
    {
        using var nativeValue = new NativeValueBuilder(value);
        var root = nativeValue.Root;
        var error = NativeMethods.BindQueryParameter(handle.DangerousGetHandle(), name, in root);
        NativeError.ThrowIfFailed(error, "query/bind");
    }

    private static void ValidateParameterName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        NativeText.ValidateNullTerminated(name, nameof(name));
    }

    private void EnsureManualBindingMode()
    {
        if (dictionaryParameterNames is not null)
        {
            throw new InvalidOperationException(
                "Individual Bind calls cannot follow parameter-dictionary execution because native bindings cannot be cleared.");
        }
    }

    private static LatticeRow ReadSingle(LatticeQueryResult result)
    {
        using (result)
        {
            if (!result.MoveNext())
                throw new InvalidOperationException("The query returned no rows.");
            var row = result.Current;
            if (result.MoveNext())
                throw new InvalidOperationException("The query returned more than one row.");
            return row;
        }
    }

    private static LatticeValue ReadScalar(LatticeRow row)
    {
        if (row.Count != 1)
            throw new InvalidOperationException($"The query returned {row.Count} columns; exactly one is required.");
        return row[0];
    }
}
