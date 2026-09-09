namespace LatticeDBSharp.Interop;

internal enum NativeErrorCode
{
    Ok = 0,
    Error = -1,
    Io = -2,
    Corruption = -3,
    NotFound = -4,
    AlreadyExists = -5,
    InvalidArgument = -6,
    TransactionAborted = -7,
    LockTimeout = -8,
    ReadOnly = -9,
    Full = -10,
    VersionMismatch = -11,
    Checksum = -12,
    OutOfMemory = -13,
    Unsupported = -14,
    ValueTooLarge = -15,
    DatabaseLocked = -16,
}

internal enum NativeTransactionMode
{
    ReadOnly = 0,
    ReadWrite = 1,
}
