using System.Runtime.InteropServices;

namespace LatticeDbSharp.Interop;

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

    [LibraryImport(LibraryName, EntryPoint = "lattice_get_nodes_by_label", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial NativeErrorCode GetNodesByLabel(
        nint database,
        string label,
        nuint labelLength,
        out nint nodeIds,
        out nuint count);

    [LibraryImport(LibraryName, EntryPoint = "lattice_get_nodes_by_label_txn", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial NativeErrorCode GetNodesByLabelTransaction(
        nint transaction,
        string label,
        nuint labelLength,
        out nint nodeIds,
        out nuint count);

    [LibraryImport(LibraryName, EntryPoint = "lattice_get_all_nodes_txn")]
    internal static partial NativeErrorCode GetAllNodesTransaction(
        nint transaction,
        out nint nodeIds,
        out nuint count);

    [LibraryImport(LibraryName, EntryPoint = "lattice_free_node_ids")]
    internal static partial void FreeNodeIds(nint nodeIds, nuint count);

    [LibraryImport(LibraryName, EntryPoint = "lattice_node_property_index_create", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial NativeErrorCode CreateNodePropertyIndex(
        nint database,
        string label,
        string property);

    [LibraryImport(LibraryName, EntryPoint = "lattice_node_property_index_drop", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial NativeErrorCode DropNodePropertyIndex(
        nint database,
        string label,
        string property);

    [LibraryImport(LibraryName, EntryPoint = "lattice_edge_property_index_create", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial NativeErrorCode CreateEdgePropertyIndex(
        nint database,
        string edgeType,
        string property);

    [LibraryImport(LibraryName, EntryPoint = "lattice_edge_property_index_drop", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial NativeErrorCode DropEdgePropertyIndex(
        nint database,
        string edgeType,
        string property);

    [LibraryImport(LibraryName, EntryPoint = "lattice_nodes_find_by_label_property", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial NativeErrorCode FindNodesByLabelProperty(
        nint transaction,
        string label,
        string property,
        in NativeValue value,
        nuint limit,
        out nint nodeIds,
        out nuint count);

    [LibraryImport(LibraryName, EntryPoint = "lattice_edges_find_by_type_property", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial NativeErrorCode FindEdgesByTypeProperty(
        nint transaction,
        string edgeType,
        string property,
        in NativeValue value,
        nuint limit,
        out nint edgeIds,
        out nuint count);

    [LibraryImport(LibraryName, EntryPoint = "lattice_serialize")]
    internal static partial NativeErrorCode SerializeDatabase(
        nint database,
        out nint bytes,
        out nuint length);

    [LibraryImport(LibraryName, EntryPoint = "lattice_deserialize")]
    internal static partial NativeErrorCode DeserializeDatabase(
        nint bytes,
        nuint length,
        in NativeOpenOptionsV4 options,
        out nint database);

    [LibraryImport(LibraryName, EntryPoint = "lattice_free_bytes")]
    internal static partial void FreeBytes(nint bytes, nuint length);

    [LibraryImport(LibraryName, EntryPoint = "lattice_edge_create", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial NativeErrorCode CreateEdge(
        nint transaction,
        ulong source,
        ulong target,
        string edgeType,
        out ulong edgeId);

    [LibraryImport(LibraryName, EntryPoint = "lattice_edge_delete", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial NativeErrorCode DeleteEdge(
        nint transaction,
        ulong source,
        ulong target,
        string edgeType);

    [LibraryImport(LibraryName, EntryPoint = "lattice_edge_set_property", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial NativeErrorCode SetEdgeProperty(
        nint transaction,
        ulong edgeId,
        string key,
        in NativeValue value);

    [LibraryImport(LibraryName, EntryPoint = "lattice_edge_get_property", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial NativeErrorCode GetEdgeProperty(
        nint transaction,
        ulong edgeId,
        string key,
        out NativeValue value);

    [LibraryImport(LibraryName, EntryPoint = "lattice_edge_remove_property", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial NativeErrorCode RemoveEdgeProperty(
        nint transaction,
        ulong edgeId,
        string key);

    [LibraryImport(LibraryName, EntryPoint = "lattice_edge_get_outgoing")]
    internal static partial NativeErrorCode GetOutgoingEdges(
        nint transaction,
        ulong nodeId,
        out nint result);

    [LibraryImport(LibraryName, EntryPoint = "lattice_edge_get_incoming")]
    internal static partial NativeErrorCode GetIncomingEdges(
        nint transaction,
        ulong nodeId,
        out nint result);

    [LibraryImport(LibraryName, EntryPoint = "lattice_edge_get_outgoing_by_type", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial NativeErrorCode GetOutgoingEdgesByType(
        nint transaction,
        ulong nodeId,
        string edgeType,
        nuint limit,
        out nint result);

    [LibraryImport(LibraryName, EntryPoint = "lattice_edge_get_incoming_by_type", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial NativeErrorCode GetIncomingEdgesByType(
        nint transaction,
        ulong nodeId,
        string edgeType,
        nuint limit,
        out nint result);

    [LibraryImport(LibraryName, EntryPoint = "lattice_edge_result_count")]
    internal static partial uint EdgeResultCount(nint result);

    [LibraryImport(LibraryName, EntryPoint = "lattice_edge_result_get_id")]
    internal static partial NativeErrorCode EdgeResultGetId(
        nint result,
        uint index,
        out ulong edgeId);

    [LibraryImport(LibraryName, EntryPoint = "lattice_edge_result_get")]
    internal static partial NativeErrorCode EdgeResultGet(
        nint result,
        uint index,
        out ulong source,
        out ulong target,
        out nint edgeType,
        out uint edgeTypeLength);

    [LibraryImport(LibraryName, EntryPoint = "lattice_edge_result_free")]
    internal static partial void FreeEdgeResult(nint result);

    [LibraryImport(LibraryName, EntryPoint = "lattice_free_edge_ids")]
    internal static partial void FreeEdgeIds(nint edgeIds, nuint count);

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

    [LibraryImport(LibraryName, EntryPoint = "lattice_query_bind_vector", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial NativeErrorCode BindQueryVector(
        nint query,
        string name,
        nint vector,
        uint dimensions);

    [LibraryImport(LibraryName, EntryPoint = "lattice_node_fts_index_create", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial NativeErrorCode CreateNodeFtsIndex(
        nint database,
        string label,
        string property);

    [LibraryImport(LibraryName, EntryPoint = "lattice_node_fts_index_drop", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial NativeErrorCode DropNodeFtsIndex(
        nint database,
        string label,
        string property);

    [LibraryImport(LibraryName, EntryPoint = "lattice_node_fts_index_exists", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial NativeErrorCode NodeFtsIndexExists(
        nint database,
        string label,
        string property,
        out byte exists);

    [LibraryImport(LibraryName, EntryPoint = "lattice_edge_fts_index_create", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial NativeErrorCode CreateEdgeFtsIndex(
        nint database,
        string edgeType,
        string property);

    [LibraryImport(LibraryName, EntryPoint = "lattice_edge_fts_index_drop", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial NativeErrorCode DropEdgeFtsIndex(
        nint database,
        string edgeType,
        string property);

    [LibraryImport(LibraryName, EntryPoint = "lattice_edge_fts_index_exists", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial NativeErrorCode EdgeFtsIndexExists(
        nint database,
        string edgeType,
        string property,
        out byte exists);

    [LibraryImport(LibraryName, EntryPoint = "lattice_fts_search", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial NativeErrorCode FtsSearch(
        nint database,
        string label,
        string property,
        string query,
        nuint queryLength,
        uint limit,
        out nint result);

    [LibraryImport(LibraryName, EntryPoint = "lattice_fts_search_txn", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial NativeErrorCode FtsSearchTransaction(
        nint transaction,
        string label,
        string property,
        string query,
        nuint queryLength,
        uint limit,
        out nint result);

    [LibraryImport(LibraryName, EntryPoint = "lattice_fts_search_fuzzy", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial NativeErrorCode FtsSearchFuzzy(
        nint database,
        string label,
        string property,
        string query,
        nuint queryLength,
        uint limit,
        uint maxDistance,
        uint minTermLength,
        out nint result);

    [LibraryImport(LibraryName, EntryPoint = "lattice_fts_search_fuzzy_txn", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial NativeErrorCode FtsSearchFuzzyTransaction(
        nint transaction,
        string label,
        string property,
        string query,
        nuint queryLength,
        uint limit,
        uint maxDistance,
        uint minTermLength,
        out nint result);

    [LibraryImport(LibraryName, EntryPoint = "lattice_fts_result_count")]
    internal static partial uint FtsResultCount(nint result);

    [LibraryImport(LibraryName, EntryPoint = "lattice_fts_result_get")]
    internal static partial NativeErrorCode FtsResultGet(
        nint result,
        uint index,
        out ulong nodeId,
        out float score);

    [LibraryImport(LibraryName, EntryPoint = "lattice_fts_result_free")]
    internal static partial void FreeFtsResult(nint result);

    [LibraryImport(LibraryName, EntryPoint = "lattice_batch_insert")]
    internal static partial NativeErrorCode BatchInsert(
        nint transaction,
        nint nodes,
        uint count,
        nint nodeIdsOut,
        out uint countOut);

    [LibraryImport(LibraryName, EntryPoint = "lattice_vector_search")]
    internal static partial NativeErrorCode VectorSearch(
        nint database,
        nint vector,
        uint dimensions,
        uint count,
        ushort efSearch,
        out nint result);

    [LibraryImport(LibraryName, EntryPoint = "lattice_vector_search_txn")]
    internal static partial NativeErrorCode VectorSearchTransaction(
        nint transaction,
        nint vector,
        uint dimensions,
        uint count,
        ushort efSearch,
        out nint result);

    [LibraryImport(LibraryName, EntryPoint = "lattice_vector_result_count")]
    internal static partial uint VectorResultCount(nint result);

    [LibraryImport(LibraryName, EntryPoint = "lattice_vector_result_get")]
    internal static partial NativeErrorCode VectorResultGet(
        nint result,
        uint index,
        out ulong nodeId,
        out float distance);

    [LibraryImport(LibraryName, EntryPoint = "lattice_vector_result_free")]
    internal static partial void FreeVectorResult(nint result);

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
