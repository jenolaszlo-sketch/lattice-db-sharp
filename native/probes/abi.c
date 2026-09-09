#include "lattice.h"

#include <stddef.h>
#include <stdio.h>

int main(void) {
    printf(
        "{\"pointerSize\":%zu,\"boolSize\":%zu,\"errorSize\":%zu,"
        "\"transactionModeSize\":%zu,\"queryStageSize\":%zu,\"valueTypeSize\":%zu,"
        "\"nodeIdSize\":%zu,\"edgeIdSize\":%zu,"
        "\"openOptionsV4\":{\"size\":%zu,\"structSize\":%zu,"
        "\"create\":%zu,\"readOnly\":%zu,\"cacheSizeMb\":%zu,"
        "\"pageSize\":%zu,\"enableVector\":%zu,"
        "\"vectorDimensions\":%zu,\"enableWal\":%zu,"
        "\"enableAdjacencyCache\":%zu,\"lock\":%zu},"
        "\"value\":{\"size\":%zu,\"type\":%zu,\"boolean\":%zu,"
        "\"integer\":%zu,\"float\":%zu,\"string\":%zu,\"bytes\":%zu,"
        "\"vector\":%zu,\"list\":%zu,\"map\":%zu},"
        "\"stringValue\":{\"size\":%zu,\"pointer\":%zu,\"length\":%zu},"
        "\"vectorValue\":{\"size\":%zu,\"pointer\":%zu,\"dimensions\":%zu},"
        "\"list\":{\"size\":%zu,\"items\":%zu,\"length\":%zu},"
        "\"map\":{\"size\":%zu,\"entries\":%zu,\"length\":%zu},"
        "\"mapEntry\":{\"size\":%zu,\"key\":%zu,\"keyLength\":%zu,\"value\":%zu}}\n",
        sizeof(void *),
        sizeof(bool),
        sizeof(lattice_error),
        sizeof(lattice_txn_mode),
        sizeof(lattice_query_error_stage),
        sizeof(lattice_value_type),
        sizeof(lattice_node_id),
        sizeof(lattice_edge_id),
        sizeof(lattice_open_options_v4),
        offsetof(lattice_open_options_v4, struct_size),
        offsetof(lattice_open_options_v4, create),
        offsetof(lattice_open_options_v4, read_only),
        offsetof(lattice_open_options_v4, cache_size_mb),
        offsetof(lattice_open_options_v4, page_size),
        offsetof(lattice_open_options_v4, enable_vector),
        offsetof(lattice_open_options_v4, vector_dimensions),
        offsetof(lattice_open_options_v4, enable_wal),
        offsetof(lattice_open_options_v4, enable_adjacency_cache),
        offsetof(lattice_open_options_v4, lock),
        sizeof(lattice_value),
        offsetof(lattice_value, type),
        offsetof(lattice_value, data.bool_val),
        offsetof(lattice_value, data.int_val),
        offsetof(lattice_value, data.float_val),
        offsetof(lattice_value, data.string_val),
        offsetof(lattice_value, data.bytes_val),
        offsetof(lattice_value, data.vector_val),
        offsetof(lattice_value, data.list_val),
        offsetof(lattice_value, data.map_val),
        sizeof(((lattice_value *)0)->data.string_val),
        offsetof(lattice_value, data.string_val.ptr) - offsetof(lattice_value, data.string_val),
        offsetof(lattice_value, data.string_val.len) - offsetof(lattice_value, data.string_val),
        sizeof(((lattice_value *)0)->data.vector_val),
        offsetof(lattice_value, data.vector_val.ptr) - offsetof(lattice_value, data.vector_val),
        offsetof(lattice_value, data.vector_val.dimensions) - offsetof(lattice_value, data.vector_val),
        sizeof(lattice_list),
        offsetof(lattice_list, items),
        offsetof(lattice_list, len),
        sizeof(lattice_map),
        offsetof(lattice_map, entries),
        offsetof(lattice_map, len),
        sizeof(lattice_map_entry),
        offsetof(lattice_map_entry, key),
        offsetof(lattice_map_entry, key_len),
        offsetof(lattice_map_entry, value));
    return 0;
}
