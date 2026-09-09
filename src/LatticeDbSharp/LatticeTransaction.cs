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
