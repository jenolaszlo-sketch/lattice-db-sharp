using System.Runtime.InteropServices;

namespace LatticeDBSharp.Interop;

internal static partial class NativeMethods
{
    internal const string LibraryName = "lattice";

    [LibraryImport(LibraryName, EntryPoint = "lattice_version")]
    internal static partial nint GetVersion();

    [LibraryImport(LibraryName, EntryPoint = "lattice_error_message")]
    internal static partial nint GetErrorMessage(NativeErrorCode error);

    [LibraryImport(LibraryName, EntryPoint = "lattice_open_v4", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial NativeErrorCode OpenV4(
        string path,
        in NativeOpenOptionsV4 options,
        out nint database);

    [LibraryImport(LibraryName, EntryPoint = "lattice_close")]
    internal static partial NativeErrorCode Close(nint database);

    [LibraryImport(LibraryName, EntryPoint = "lattice_begin")]
    internal static partial NativeErrorCode Begin(
        nint database,
        NativeTransactionMode mode,
        out nint transaction);

    [LibraryImport(LibraryName, EntryPoint = "lattice_commit")]
    internal static partial NativeErrorCode Commit(nint transaction);

    [LibraryImport(LibraryName, EntryPoint = "lattice_rollback")]
    internal static partial NativeErrorCode Rollback(nint transaction);

    [LibraryImport(LibraryName, EntryPoint = "lattice_node_create", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial NativeErrorCode CreateNode(
        nint transaction,
        string? label,
        out ulong nodeId);

    [LibraryImport(LibraryName, EntryPoint = "lattice_node_exists")]
    internal static partial NativeErrorCode NodeExists(
        nint transaction,
        ulong nodeId,
        out byte exists);

    [LibraryImport(LibraryName, EntryPoint = "lattice_edge_create", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial NativeErrorCode CreateEdge(
        nint transaction,
        ulong source,
        ulong target,
        string edgeType,
        out ulong edgeId);

    [LibraryImport(LibraryName, EntryPoint = "lattice_node_add_label", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial NativeErrorCode AddNodeLabel(
        nint transaction,
        ulong nodeId,
        string label);

    [LibraryImport(LibraryName, EntryPoint = "lattice_node_remove_label", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial NativeErrorCode RemoveNodeLabel(
        nint transaction,
        ulong nodeId,
        string label);

    [LibraryImport(LibraryName, EntryPoint = "lattice_node_delete")]
    internal static partial NativeErrorCode DeleteNode(
        nint transaction,
        ulong nodeId);

    [LibraryImport(LibraryName, EntryPoint = "lattice_node_set_property", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial NativeErrorCode SetNodeProperty(
        nint transaction,
        ulong nodeId,
        string key,
        in NativeValue value);

    [LibraryImport(LibraryName, EntryPoint = "lattice_node_get_property", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial NativeErrorCode GetNodeProperty(
        nint transaction,
        ulong nodeId,
        string key,
        out NativeValue value);

    [LibraryImport(LibraryName, EntryPoint = "lattice_value_free")]
    internal static partial void FreeValue(ref NativeValue value);

    [LibraryImport(LibraryName, EntryPoint = "lattice_node_get_labels")]
    internal static partial NativeErrorCode GetNodeLabels(
        nint transaction,
        ulong nodeId,
        out nint labels);

    [LibraryImport(LibraryName, EntryPoint = "lattice_free_string")]
    internal static partial void FreeString(nint value);

    [LibraryImport(LibraryName, EntryPoint = "lattice_node_set_vector", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial NativeErrorCode SetNodeVector(
        nint transaction,
        ulong nodeId,
        string? key,
        nint vector,
        uint dimensions);

    [LibraryImport(LibraryName, EntryPoint = "lattice_query_prepare", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial NativeErrorCode PrepareQuery(
        nint database,
        string cypher,
        out nint query);

    [LibraryImport(LibraryName, EntryPoint = "lattice_query_bind", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial NativeErrorCode BindQueryParameter(
        nint query,
        string name,
        in NativeValue value);

    [LibraryImport(LibraryName, EntryPoint = "lattice_query_execute")]
    internal static partial NativeErrorCode ExecuteQuery(
        nint query,
        nint transaction,
        out nint result);

    [LibraryImport(LibraryName, EntryPoint = "lattice_query_writes")]
    internal static partial byte QueryWrites(nint query);

    [LibraryImport(LibraryName, EntryPoint = "lattice_query_free")]
    internal static partial void FreeQuery(nint query);

    [LibraryImport(LibraryName, EntryPoint = "lattice_query_last_error_stage")]
    internal static partial NativeQueryErrorStage GetQueryErrorStage(nint query);

    [LibraryImport(LibraryName, EntryPoint = "lattice_query_last_error_message")]
    internal static partial nint GetQueryErrorMessage(nint query);

    [LibraryImport(LibraryName, EntryPoint = "lattice_query_last_error_code")]
    internal static partial nint GetQueryErrorCode(nint query);

    [LibraryImport(LibraryName, EntryPoint = "lattice_query_last_error_has_location")]
    internal static partial byte QueryErrorHasLocation(nint query);

    [LibraryImport(LibraryName, EntryPoint = "lattice_query_last_error_line")]
    internal static partial uint GetQueryErrorLine(nint query);

    [LibraryImport(LibraryName, EntryPoint = "lattice_query_last_error_column")]
    internal static partial uint GetQueryErrorColumn(nint query);

    [LibraryImport(LibraryName, EntryPoint = "lattice_query_last_error_length")]
    internal static partial uint GetQueryErrorLength(nint query);

    [LibraryImport(LibraryName, EntryPoint = "lattice_result_next")]
    internal static partial byte ResultNext(nint result);

    [LibraryImport(LibraryName, EntryPoint = "lattice_result_column_count")]
    internal static partial uint ResultColumnCount(nint result);

    [LibraryImport(LibraryName, EntryPoint = "lattice_result_column_name")]
    internal static partial nint ResultColumnName(nint result, uint index);

    [LibraryImport(LibraryName, EntryPoint = "lattice_result_get")]
    internal static partial NativeErrorCode ResultGet(
        nint result,
        uint index,
        out NativeValue value);

    [LibraryImport(LibraryName, EntryPoint = "lattice_result_free")]
    internal static partial void FreeResult(nint result);
}
