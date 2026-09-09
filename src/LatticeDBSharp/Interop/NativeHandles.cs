using Microsoft.Win32.SafeHandles;

namespace LatticeDBSharp.Interop;

internal sealed class SafeLatticeDatabaseHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    internal SafeLatticeDatabaseHandle(nint handle)
        : base(ownsHandle: true)
    {
        SetHandle(handle);
    }

    internal NativeErrorCode CloseChecked()
    {
        if (IsInvalid || IsClosed)
        {
            return NativeErrorCode.Ok;
        }

        var error = NativeMethods.Close(handle);
        if (error != NativeErrorCode.InvalidArgument)
        {
            SetHandleAsInvalid();
        }

        return error;
    }

    protected override bool ReleaseHandle()
    {
        try
        {
            var error = NativeMethods.Close(handle);
            return error != NativeErrorCode.InvalidArgument;
        }
        catch
        {
            // ReleaseHandle runs from a critical finalizer and must never let a
            // P/Invoke exception escape during process teardown.
            return false;
        }
    }
}

internal sealed class SafeLatticeTransactionHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    private readonly SafeLatticeDatabaseHandle databaseHandle;
    private readonly Action releaseOwner;
    private int databaseReferenceHeld;

    internal SafeLatticeTransactionHandle(
        nint handle,
        SafeLatticeDatabaseHandle databaseHandle,
        Action releaseOwner)
        : base(ownsHandle: true)
    {
        this.databaseHandle = databaseHandle;
        this.releaseOwner = releaseOwner;

        var addedReference = false;
        try
        {
            databaseHandle.DangerousAddRef(ref addedReference);
            if (!addedReference)
            {
                throw new ObjectDisposedException(nameof(databaseHandle));
            }

            Volatile.Write(ref databaseReferenceHeld, 1);
            SetHandle(handle);
        }
        catch
        {
            if (addedReference)
            {
                databaseHandle.DangerousRelease();
            }

            throw;
        }
    }

    internal void MarkConsumed()
    {
        SetHandleAsInvalid();
        ReleaseDatabaseReference();
    }

    protected override bool ReleaseHandle()
    {
        try
        {
            _ = NativeMethods.Rollback(handle);
        }
        catch
        {
            // Native rollback is best-effort from the finalizer path. The
            // managed owner is still released below even if the library has
            // already started unloading.
        }
        finally
        {
            try
            {
                ReleaseDatabaseReference();
            }
            catch
            {
                // Parent SafeHandle release is also best-effort from this
                // critical-finalizer path.
            }

            try
            {
                releaseOwner();
            }
            catch
            {
                // Never allow managed owner bookkeeping to escape a critical
                // finalizer during process teardown.
            }
        }

        return true;
    }

    private void ReleaseDatabaseReference()
    {
        if (Interlocked.Exchange(ref databaseReferenceHeld, 0) == 1)
        {
            databaseHandle.DangerousRelease();
        }
    }
}

internal sealed class SafeLatticeQueryHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    private readonly SafeLatticeDatabaseHandle databaseHandle;
    private readonly Action releaseOwner;
    private int databaseReferenceHeld;

    internal SafeLatticeQueryHandle(
        nint handle,
        SafeLatticeDatabaseHandle databaseHandle,
        Action releaseOwner)
        : base(ownsHandle: true)
    {
        this.databaseHandle = databaseHandle;
        this.releaseOwner = releaseOwner;

        var addedReference = false;
        try
        {
            databaseHandle.DangerousAddRef(ref addedReference);
            if (!addedReference)
            {
                throw new ObjectDisposedException(nameof(databaseHandle));
            }

            Volatile.Write(ref databaseReferenceHeld, 1);
            SetHandle(handle);
        }
        catch
        {
            if (addedReference)
            {
                databaseHandle.DangerousRelease();
            }

            throw;
        }
    }

    protected override bool ReleaseHandle()
    {
        try
        {
            NativeMethods.FreeQuery(handle);
        }
        catch
        {
        }
        finally
        {
            try
            {
                ReleaseDatabaseReference();
            }
            catch
            {
                // Parent release is best-effort from the critical-finalizer path.
            }

            try
            {
                releaseOwner();
            }
            catch
            {
                // Never allow owner bookkeeping to escape a critical finalizer.
            }
        }

        return true;
    }

    private void ReleaseDatabaseReference()
    {
        if (Interlocked.Exchange(ref databaseReferenceHeld, 0) == 1)
        {
            databaseHandle.DangerousRelease();
        }
    }
}

internal sealed class SafeLatticeResultHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    private readonly SafeLatticeQueryHandle queryHandle;
    private readonly Action releaseOwner;
    private int queryReferenceHeld;

    internal SafeLatticeResultHandle(
        nint handle,
        SafeLatticeQueryHandle queryHandle,
        Action releaseOwner)
        : base(ownsHandle: true)
    {
        this.queryHandle = queryHandle;
        this.releaseOwner = releaseOwner;

        var addedReference = false;
        try
        {
            queryHandle.DangerousAddRef(ref addedReference);
            if (!addedReference)
            {
                throw new ObjectDisposedException(nameof(queryHandle));
            }

            Volatile.Write(ref queryReferenceHeld, 1);
            SetHandle(handle);
        }
        catch
        {
            if (addedReference)
            {
                queryHandle.DangerousRelease();
            }

            throw;
        }
    }

    protected override bool ReleaseHandle()
    {
        try
        {
            NativeMethods.FreeResult(handle);
        }
        catch
        {
        }
        finally
        {
            try
            {
                ReleaseQueryReference();
            }
            catch
            {
                // Parent release is best-effort from the critical-finalizer path.
            }

            try
            {
                releaseOwner();
            }
            catch
            {
                // Never allow owner bookkeeping to escape a critical finalizer.
            }
        }

        return true;
    }

    private void ReleaseQueryReference()
    {
        if (Interlocked.Exchange(ref queryReferenceHeld, 0) == 1)
        {
            queryHandle.DangerousRelease();
        }
    }
}
