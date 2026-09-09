using System.Collections.ObjectModel;
using LatticeDbSharp.Interop;

namespace LatticeDbSharp;

/// <summary>A single-use result cursor whose rows are detached snapshots.</summary>
public sealed class LatticeQueryResult : IDisposable
{
    private readonly object gate = new();
    private readonly LatticeQuery query;
    private readonly SafeLatticeResultHandle handle;
    private bool disposed;
    private LatticeRow? current;

    internal LatticeQueryResult(
        LatticeQuery query,
        SafeLatticeResultHandle handle,
        IReadOnlyList<string> columns)
    {
        this.query = query;
        this.handle = handle;
        Columns = columns;
    }

    /// <summary>Column names in native result order, including duplicates.</summary>
    public IReadOnlyList<string> Columns { get; }

    /// <summary>The detached current row after a successful <see cref="MoveNext"/>.</summary>
    public LatticeRow Current
    {
        get
        {
            lock (gate)
            {
                EnsureActive();
                return current ?? throw new InvalidOperationException("The result cursor is not positioned on a row.");
            }
        }
    }

    /// <summary>Advances to the next row.</summary>
    public bool MoveNext()
    {
        lock (gate)
        {
            EnsureActive();
            if (NativeMethods.ResultNext(handle.DangerousGetHandle()) == 0)
            {
                current = null;
                return false;
            }

            var values = new LatticeValue[Columns.Count];
            for (var index = 0; index < values.Length; index++)
            {
                var error = NativeMethods.ResultGet(
                    handle.DangerousGetHandle(),
                    (uint)index,
                    out var nativeValue);
                NativeError.ThrowIfFailed(error, "result/get");
                values[index] = NativeValueConversion.ToManaged(nativeValue);
            }

            current = new LatticeRow(Columns, Array.AsReadOnly(values));
            return true;
        }
    }

    /// <summary>Consumes the remaining cursor and returns detached rows.</summary>
    public IReadOnlyList<LatticeRow> ReadAll()
    {
        var rows = new List<LatticeRow>();
        while (MoveNext())
        {
            rows.Add(Current);
        }

        return new ReadOnlyCollection<LatticeRow>(rows);
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

            handle.Dispose();
            disposed = true;
        }
    }

    private void EnsureActive()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }
}
