using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Text.Json;
using LatticeDbSharp.Interop;
using LatticeDbSharp.NativeWorker;
using Xunit;

namespace LatticeDbSharp.IntegrationTests;

public sealed class NativeFeasibilityTests
{
#if LATTICEDBSHARP_NATIVE_TESTS
    [Fact]
#else
    [Fact(Skip = "Enabled after the pinned LatticeDB native library is built and staged.")]
#endif
    public void Pinned_native_library_opens_and_reports_its_version()
    {
        Assert.True(LatticeNative.IsAvailable);
        Assert.Equal(LatticeNative.PinnedVersion, LatticeNative.Version);
        Assert.Equal("0.15.0", LatticeNative.Version);
        Assert.NotNull(LatticeNative.LibraryPath);
        Assert.True(Path.IsPathFullyQualified(LatticeNative.LibraryPath));
    }

#if LATTICEDBSHARP_NATIVE_TESTS
    [Fact]
#else
    [Fact(Skip = "Enabled after the pinned LatticeDB native library is built and staged.")]
#endif
    public void Native_library_selection_does_not_change_after_database_open()
    {
        using var temporary = TemporaryDirectory.Create();
        using var worker = StartWorker("resolver", Path.Combine(temporary.Path, "resolver.db"));
    }

#if LATTICEDBSHARP_NATIVE_TESTS
    [Fact]
#else
    [Fact(Skip = "Enabled after the pinned ABI probe has run.")]
#endif
    public void Managed_open_options_match_the_pinned_native_abi()
    {
        var path = Environment.GetEnvironmentVariable("LATTICEDBSHARP_ABI_MANIFEST");
        Assert.False(string.IsNullOrWhiteSpace(path));
        Assert.True(Path.IsPathFullyQualified(path));

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        var options = root.GetProperty("openOptionsV4");
        var value = root.GetProperty("value");
        var stringValue = root.GetProperty("stringValue");
        var vectorValue = root.GetProperty("vectorValue");
        var list = root.GetProperty("list");
        var map = root.GetProperty("map");
        var mapEntry = root.GetProperty("mapEntry");

        Assert.Equal(nint.Size, root.GetProperty("pointerSize").GetInt32());
        Assert.Equal(1, root.GetProperty("boolSize").GetInt32());
        Assert.Equal(sizeof(int), root.GetProperty("errorSize").GetInt32());
        Assert.Equal(sizeof(int), root.GetProperty("transactionModeSize").GetInt32());
        Assert.Equal(sizeof(int), root.GetProperty("queryStageSize").GetInt32());
        Assert.Equal(sizeof(int), root.GetProperty("valueTypeSize").GetInt32());
        Assert.Equal(sizeof(ulong), root.GetProperty("nodeIdSize").GetInt32());
        Assert.Equal(sizeof(ulong), root.GetProperty("edgeIdSize").GetInt32());
        Assert.Equal(Marshal.SizeOf<NativeOpenOptionsV4>(), options.GetProperty("size").GetInt32());
        AssertOffset<NativeOpenOptionsV4>(options, "structSize", nameof(NativeOpenOptionsV4.StructSize));
        AssertOffset<NativeOpenOptionsV4>(options, "create", nameof(NativeOpenOptionsV4.Create));
        AssertOffset<NativeOpenOptionsV4>(options, "readOnly", nameof(NativeOpenOptionsV4.ReadOnly));
        AssertOffset<NativeOpenOptionsV4>(options, "cacheSizeMb", nameof(NativeOpenOptionsV4.CacheSizeMb));
        AssertOffset<NativeOpenOptionsV4>(options, "pageSize", nameof(NativeOpenOptionsV4.PageSize));
        AssertOffset<NativeOpenOptionsV4>(options, "enableVector", nameof(NativeOpenOptionsV4.EnableVector));
        AssertOffset<NativeOpenOptionsV4>(options, "vectorDimensions", nameof(NativeOpenOptionsV4.VectorDimensions));
        AssertOffset<NativeOpenOptionsV4>(options, "enableWal", nameof(NativeOpenOptionsV4.EnableWal));
        AssertOffset<NativeOpenOptionsV4>(options, "enableAdjacencyCache", nameof(NativeOpenOptionsV4.EnableAdjacencyCache));
        AssertOffset<NativeOpenOptionsV4>(options, "lock", nameof(NativeOpenOptionsV4.Lock));

        Assert.Equal(Marshal.SizeOf<NativeValue>(), value.GetProperty("size").GetInt32());
        AssertOffset<NativeValue>(value, "type", nameof(NativeValue.Type));
        AssertOffset<NativeValue>(value, "boolean", nameof(NativeValue.BooleanValue));
        AssertOffset<NativeValue>(value, "integer", nameof(NativeValue.IntegerValue));
        AssertOffset<NativeValue>(value, "float", nameof(NativeValue.FloatValue));
        AssertOffset<NativeValue>(value, "string", nameof(NativeValue.StringValue));
        AssertOffset<NativeValue>(value, "bytes", nameof(NativeValue.BytesValue));
        AssertOffset<NativeValue>(value, "vector", nameof(NativeValue.VectorValue));
        AssertOffset<NativeValue>(value, "list", nameof(NativeValue.ListValue));
        AssertOffset<NativeValue>(value, "map", nameof(NativeValue.MapValue));
        AssertStruct<NativeStringValue>(stringValue, ("pointer", nameof(NativeStringValue.Pointer)), ("length", nameof(NativeStringValue.Length)));
        AssertStruct<NativeVectorValue>(vectorValue, ("pointer", nameof(NativeVectorValue.Pointer)), ("dimensions", nameof(NativeVectorValue.Dimensions)));
        AssertStruct<NativeListValue>(list, ("items", nameof(NativeListValue.Items)), ("length", nameof(NativeListValue.Length)));
        AssertStruct<NativeMapValue>(map, ("entries", nameof(NativeMapValue.Entries)), ("length", nameof(NativeMapValue.Length)));
        AssertStruct<NativeMapEntry>(mapEntry, ("key", nameof(NativeMapEntry.Key)), ("keyLength", nameof(NativeMapEntry.KeyLength)), ("value", nameof(NativeMapEntry.Value)));

        var defaults = NativeOpenOptionsV4.CreateDefault(create: true, readOnly: false);
        Assert.Equal((nuint)Marshal.SizeOf<NativeOpenOptionsV4>(), defaults.StructSize);
        Assert.Equal(1, defaults.Create);
        Assert.Equal(0, defaults.ReadOnly);
        Assert.Equal(1, defaults.EnableWal);
        Assert.Equal(1, defaults.Lock);
    }

#if LATTICEDBSHARP_NATIVE_TESTS
    [Fact]
#else
    [Fact(Skip = "Enabled after the pinned LatticeDB native library is built and staged.")]
#endif
    public void Memory_database_commits_and_reads_a_minimal_graph()
    {
        using var database = LatticeDatabase.OpenMemory();
        LatticeNodeId first;
        LatticeNodeId second;

        using (var write = database.BeginWriteTransaction())
        {
            first = write.CreateNode("Person");
            second = write.CreateNode("Person");
            var edge = write.CreateEdge(first, second, "KNOWS");

            Assert.NotEqual(default, first);
            Assert.NotEqual(default, second);
            Assert.NotEqual(default, edge);
            write.Commit();
        }

        using var read = database.BeginReadTransaction();
        Assert.True(read.NodeExists(first));
        Assert.True(read.NodeExists(second));
        read.Commit();
    }

#if LATTICEDBSHARP_NATIVE_TESTS
    [Fact]
#else
    [Fact(Skip = "Enabled after the pinned LatticeDB native library is built and staged.")]
#endif
    public void Rollback_hides_created_node_and_close_rejects_active_children()
    {
        var database = LatticeDatabase.OpenMemory();
        var write = database.BeginWriteTransaction();
        var rolledBack = write.CreateNode("Temporary");

        var closeError = Assert.Throws<InvalidOperationException>(database.Close);
        Assert.Contains("transaction", closeError.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(database.IsClosed);

        write.Rollback();
        Assert.True(write.IsCompleted);

        using (var read = database.BeginReadTransaction())
        {
            Assert.False(read.NodeExists(rolledBack));
        }

        database.Close();
        database.Close();
        Assert.True(database.IsClosed);
    }

#if LATTICEDBSHARP_NATIVE_TESTS
    [Fact]
#else
    [Fact(Skip = "Enabled after the pinned LatticeDB native library is built and staged.")]
#endif
    public void File_database_survives_close_and_reopen()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"latticedbsharp-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "graph.ltdb");
        Directory.CreateDirectory(directory);

        try
        {
            LatticeNodeId node;
            using (var database = LatticeDatabase.Open(path, new LatticeDatabaseOptions { Create = true }))
            using (var write = database.BeginWriteTransaction())
            {
                node = write.CreateNode("Persisted");
                write.Commit();
            }

            using var reopened = LatticeDatabase.Open(path);
            using var read = reopened.BeginReadTransaction();
            Assert.True(read.NodeExists(node));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

#if LATTICEDBSHARP_NATIVE_TESTS
    [Fact]
#else
    [Fact(Skip = "Enabled after the pinned LatticeDB native library is built and staged.")]
#endif
    public void Read_only_transactions_reject_mutation_before_native_dispatch()
    {
        using var database = LatticeDatabase.OpenMemory();
        using var read = database.BeginReadTransaction();

        Assert.Throws<InvalidOperationException>(() => read.CreateNode("Nope"));
    }

#if LATTICEDBSHARP_NATIVE_TESTS
    [Fact]
#else
    [Fact(Skip = "Enabled after the pinned LatticeDB native library is built and staged.")]
#endif
    public void Native_node_graph_operations_round_trip_labels_properties_and_vectors()
    {
        using var database = LatticeDatabase.OpenMemory(new LatticeDatabaseOptions
        {
            EnableVector = true,
            VectorDimensions = 3,
        });

        LatticeNodeId node;
        using (var write = database.BeginWriteTransaction())
        {
            node = write.CreateNode("Person");
            write.AddLabel(node, "Employee");
            write.SetProperty(
                node,
                "payload",
                LatticeValue.From(new Dictionary<string, LatticeValue>(StringComparer.Ordinal)
                {
                    ["name"] = LatticeValue.From("Ada"),
                    ["scores"] = LatticeValue.From(new[]
                    {
                        LatticeValue.From(1L),
                        LatticeValue.From(new Dictionary<string, LatticeValue>
                        {
                            ["ok"] = LatticeValue.From(true),
                        }),
                    }),
                }));
            write.SetVector(node, new float[] { 1, 2, 3 });

            Assert.Equal(new[] { "Person", "Employee" }, write.GetLabels(node));
            var payload = write.GetProperty(node, "payload").AsMap();
            Assert.Equal("Ada", payload["name"].AsString());
            Assert.True(payload["scores"].AsList()[1].AsMap()["ok"].AsBoolean());
            Assert.True(write.TryGetProperty(node, "payload", out _));
            Assert.False(write.TryGetProperty(node, "missing", out _));

            write.RemoveLabel(node, "Employee");
            Assert.Equal(new[] { "Person" }, write.GetLabels(node));
            write.Commit();
        }

        using (var read = database.BeginReadTransaction())
        {
            Assert.True(read.NodeExists(node));
            Assert.Equal(new[] { "Person" }, read.GetLabels(node));
            Assert.Throws<KeyNotFoundException>(() => read.GetProperty(node, "missing"));
            read.Commit();
        }

        using (var delete = database.BeginWriteTransaction())
        {
            delete.DeleteNode(node);
            Assert.False(delete.NodeExists(node));
            delete.Commit();
        }
    }

#if LATTICEDBSHARP_NATIVE_TESTS
    [Fact]
#else
    [Fact(Skip = "Enabled after the pinned LatticeDB native library is built and staged.")]
#endif
    public void Native_node_graph_mutations_rollback_and_reject_ambiguous_labels()
    {
        using var database = LatticeDatabase.OpenMemory();
        LatticeNodeId node;
        using (var create = database.BeginWriteTransaction())
        {
            node = create.CreateNode("Stable");
            create.Commit();
        }

        using (var rollback = database.BeginWriteTransaction())
        {
            rollback.AddLabel(node, "Temporary");
            rollback.SetProperty(node, "name", LatticeValue.From("not committed"));
            rollback.DeleteNode(node);
            rollback.Rollback();
        }

        using (var read = database.BeginReadTransaction())
        {
            Assert.True(read.NodeExists(node));
            Assert.Equal(new[] { "Stable" }, read.GetLabels(node));
            Assert.False(read.TryGetProperty(node, "name", out _));
            read.Commit();
        }

        using var write = database.BeginWriteTransaction();
        Assert.Throws<ArgumentException>(() => write.AddLabel(node, "contains,comma"));
        Assert.Throws<ArgumentException>(() => write.RemoveLabel(node, "contains,comma"));
        Assert.Throws<ArgumentException>(() => write.CreateNode("contains,comma"));
        write.Rollback();
    }

#if LATTICEDBSHARP_NATIVE_TESTS
    [Fact]
#else
    [Fact(Skip = "Enabled after the pinned LatticeDB native library is built and staged.")]
#endif
    public void Native_node_graph_operations_reject_read_only_and_disposed_usage()
    {
        using var database = LatticeDatabase.OpenMemory();
        LatticeNodeId node;
        using (var write = database.BeginWriteTransaction())
        {
            node = write.CreateNode();
            write.Commit();
        }

        using var read = database.BeginReadTransaction();
        Assert.Throws<InvalidOperationException>(() => read.AddLabel(node, "Nope"));
        Assert.Throws<InvalidOperationException>(() => read.RemoveLabel(node, "Nope"));
        Assert.Throws<InvalidOperationException>(() => read.DeleteNode(node));
        Assert.Throws<InvalidOperationException>(() => read.SetProperty(node, "name", LatticeValue.From("Nope")));
        Assert.Throws<InvalidOperationException>(() => read.SetVector(node, new float[] { 1 }));
        read.Rollback();

        Assert.Throws<ObjectDisposedException>(() => read.GetLabels(node));
        Assert.Throws<ObjectDisposedException>(() => read.TryGetProperty(node, "name", out _));
    }

#if LATTICEDBSHARP_NATIVE_TESTS
    [Fact]
#else
    [Fact(Skip = "Enabled after the pinned LatticeDB native library is built and staged.")]
#endif
    public void Finalized_transaction_releases_database_child_ownership()
    {
        var database = LatticeDatabase.OpenMemory();
        var transaction = AbandonWriteTransaction(database);

        for (var attempt = 0; attempt < 20 && transaction.IsAlive; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        Assert.False(transaction.IsAlive);
        database.Close();
        Assert.True(database.IsClosed);
    }

#if LATTICEDBSHARP_NATIVE_TESTS
    [Fact]
#else
    [Fact(Skip = "Enabled after the pinned LatticeDB native library is built and staged.")]
#endif
    public void Second_writer_is_rejected_until_the_first_writer_rolls_back()
    {
        using var database = LatticeDatabase.OpenMemory();
        using var first = database.BeginWriteTransaction();

        var exception = Assert.Throws<LatticeException>(() => database.BeginWriteTransaction());
        Assert.Equal((int)NativeErrorCode.LockTimeout, exception.NativeErrorCode);

        first.Rollback();
        using var second = database.BeginWriteTransaction();
        second.Rollback();
    }

#if LATTICEDBSHARP_NATIVE_TESTS
    [Fact]
#else
    [Fact(Skip = "Enabled after the pinned LatticeDB native library is built and staged.")]
#endif
    public void Scoped_transaction_helpers_commit_and_rollback_without_lazy_owner_escape()
    {
        using var database = LatticeDatabase.OpenMemory();
        var created = database.ExecuteWrite(transaction => transaction.CreateNode("Scoped"));

        database.ExecuteRead(transaction => Assert.True(transaction.NodeExists(created)));

        var expected = new InvalidOperationException("callback failure");
        Action<LatticeTransaction> failingCallback = _ => throw expected;
        var actual = Assert.Throws<InvalidOperationException>(() => database.ExecuteWrite(failingCallback));
        Assert.Same(expected, actual);

        using var query = database.Prepare("MATCH (n:Scoped) RETURN 1 AS value");
        var escaped = database.ExecuteRead(transaction => query.ExecuteAll(transaction));
        Assert.Single(escaped);
        Assert.Equal(1L, escaped[0][0].AsInt64());

        Assert.Throws<InvalidOperationException>(() =>
            database.ExecuteRead(transaction => query.Execute(transaction)));
    }

#if LATTICEDBSHARP_NATIVE_TESTS
    [Fact]
#else
    [Fact(Skip = "Enabled after the pinned LatticeDB native library is built and staged.")]
#endif
    public void Prepared_query_exposes_advisory_write_classification_and_disposed_behavior()
    {
        using var database = LatticeDatabase.OpenMemory();
        using var read = database.Prepare("MATCH (n:Missing) RETURN n.name AS value");
        using var write = database.Prepare("CREATE (n:Value)");

        Assert.False(read.MayWrite);
        Assert.True(write.MayWrite);

        read.Dispose();
        Assert.Throws<ObjectDisposedException>(() => read.MayWrite);
    }

#if LATTICEDBSHARP_NATIVE_TESTS
    [Fact]
#else
    [Fact(Skip = "Enabled after the pinned LatticeDB native library is built and staged.")]
#endif
    public void Prepared_query_binds_parameters_and_preserves_result_column_order()
    {
        using var database = LatticeDatabase.OpenMemory();
        using (var create = database.Prepare("CREATE (n:Value {name: $name, number: $number})"))
        using (var write = database.BeginWriteTransaction())
        {
            create.Bind("name", LatticeValue.From("alpha"));
            create.Bind("number", LatticeValue.From(42L));
            using (create.Execute(write))
            {
            }

            write.Commit();
        }

        using var query = database.Prepare(
            "MATCH (n:Value) WHERE n.name = $name RETURN n.number AS number, n.name AS name");
        query.Bind("name", LatticeValue.From("alpha"));

        using var transaction = database.BeginReadTransaction();
        using var result = query.Execute(transaction);

        Assert.Equal(new[] { "number", "name" }, result.Columns);
        Assert.True(result.MoveNext());
        Assert.Equal(42L, result.Current["number"].AsInt64());
        Assert.Equal("alpha", result.Current["name"].AsString());
        Assert.False(result.MoveNext());
        transaction.Commit();
    }

#if LATTICEDBSHARP_NATIVE_TESTS
    [Fact]
#else
    [Fact(Skip = "Enabled after the pinned LatticeDB native library is built and staged.")]
#endif
    public void Query_conveniences_enforce_cardinality_and_complete_parameter_sets()
    {
        using var database = LatticeDatabase.OpenMemory();
        using (var write = database.BeginWriteTransaction())
        {
            var single = write.CreateNode("Single");
            write.SetProperty(single, "value", LatticeValue.From(7L));
            write.CreateNode("Value");
            write.CreateNode("Value");
            write.Commit();
        }

        using var scalar = database.Prepare(
            "MATCH (n:Single) WHERE n.value = $value RETURN n.value AS value");
        using var read = database.BeginReadTransaction();
        Assert.Equal(7L, scalar.ExecuteScalar(
            read,
            new Dictionary<string, LatticeValue> { ["value"] = LatticeValue.From(7L) }).AsInt64());
        Assert.Throws<InvalidOperationException>(() => scalar.Execute(
            read,
            new Dictionary<string, LatticeValue> { ["different"] = LatticeValue.From(8L) }));
        Assert.Throws<InvalidOperationException>(() => scalar.Bind("value", LatticeValue.From(9L)));

        using var multiple = database.Prepare("MATCH (n:Value) RETURN n");
        Assert.Throws<InvalidOperationException>(() => multiple.ExecuteSingle(read));

        using var twoColumns = database.Prepare(
            "MATCH (n:Single) RETURN n.value AS first, n.value AS second");
        Assert.Throws<InvalidOperationException>(() => twoColumns.ExecuteScalar(read));
        read.Commit();
    }

#if LATTICEDBSHARP_NATIVE_TESTS
    [Fact]
#else
    [Fact(Skip = "Enabled after the pinned LatticeDB native library is built and staged.")]
#endif
    public void Query_result_must_be_disposed_before_its_query_and_database()
    {
        using var database = LatticeDatabase.OpenMemory();
        using var query = database.Prepare("MATCH (n:Missing) RETURN n.name AS value");
        using var transaction = database.BeginReadTransaction();
        var result = query.Execute(transaction);

        Assert.Throws<InvalidOperationException>(query.Dispose);
        result.Dispose();
        query.Dispose();
        transaction.Commit();
    }

#if LATTICEDBSHARP_NATIVE_TESTS
    [Fact]
#else
    [Fact(Skip = "Enabled after the pinned LatticeDB native library is built and staged.")]
#endif
    public void Prepared_write_query_observes_explicit_commit_and_rollback()
    {
        using var database = LatticeDatabase.OpenMemory();
        using var writeQuery = database.Prepare("CREATE (n:Person {name: $name})");

        using (var committed = database.BeginWriteTransaction())
        {
            using (writeQuery.Execute(
                committed,
                new Dictionary<string, LatticeValue> { ["name"] = LatticeValue.From("committed") }))
            {
            }

            committed.Commit();
        }

        using (var rolledBack = database.BeginWriteTransaction())
        {
            using (writeQuery.Execute(
                rolledBack,
                new Dictionary<string, LatticeValue> { ["name"] = LatticeValue.From("rolled-back") }))
            {
            }

            rolledBack.Rollback();
        }

        using var readQuery = database.Prepare("MATCH (n:Person) RETURN n.name AS name ORDER BY n.name");
        using var read = database.BeginReadTransaction();
        using var result = readQuery.Execute(read);
        var rows = result.ReadAll();

        Assert.Single(rows);
        Assert.Equal("committed", rows[0]["name"].AsString());
        read.Commit();
    }

#if LATTICEDBSHARP_NATIVE_TESTS
    [Fact]
#else
    [Fact(Skip = "Enabled after the pinned LatticeDB native library is built and staged.")]
#endif
    public void Nested_list_and_map_values_round_trip_as_detached_snapshots()
    {
        using var database = LatticeDatabase.OpenMemory();
        using var create = database.Prepare("CREATE (n:Value {payload: $payload})");
        create.Bind(
            "payload",
            LatticeValue.From(new Dictionary<string, LatticeValue>(StringComparer.Ordinal)
            {
                ["enabled"] = LatticeValue.From(true),
                ["items"] = LatticeValue.From(new[]
                {
                    LatticeValue.From(1L),
                    LatticeValue.From("two"),
                }),
            }));

        using (var write = database.BeginWriteTransaction())
        {
            using (create.Execute(write))
            {
            }

            write.Commit();
        }

        using var query = database.Prepare("MATCH (n:Value) RETURN n.payload AS payload");
        using var read = database.BeginReadTransaction();
        using var result = query.Execute(read);
        Assert.True(result.MoveNext());

        var payload = result.Current["payload"].AsMap();
        Assert.True(payload["enabled"].AsBoolean());
        var items = payload["items"].AsList();
        Assert.Equal(1L, items[0].AsInt64());
        Assert.Equal("two", items[1].AsString());
        Assert.False(result.MoveNext());
        read.Commit();
    }

#if LATTICEDBSHARP_NATIVE_TESTS
    [Fact]
#else
    [Fact(Skip = "Enabled after the pinned LatticeDB native library is built and staged.")]
#endif
    public void Invalid_cypher_preserves_structured_native_diagnostics()
    {
        using var database = LatticeDatabase.OpenMemory();
        using var query = database.Prepare("MATCH (n RETURN n");
        using var read = database.BeginReadTransaction();

        var exception = Assert.Throws<LatticeQueryException>(() => query.Execute(read));
        Assert.Equal(LatticeQueryStage.Parse, exception.Stage);
        Assert.False(string.IsNullOrWhiteSpace(exception.Message));
        Assert.True(exception.Line is null or > 0);
        Assert.True(exception.Column is null or > 0);
        Assert.True(exception.Length is null or >= 0);
    }

#if LATTICEDBSHARP_NATIVE_TESTS
    [Fact]
#else
    [Fact(Skip = "Enabled after the pinned LatticeDB native library is built and staged.")]
#endif
    public void Native_file_locks_are_cross_process_and_release_after_kill()
    {
        using var directory = TemporaryDirectory.Create();
        var path = Path.Combine(directory.Path, "locking.ltdb");
        using var writer = StartWorker("hold", path);

        var writerConflict = Assert.Throws<LatticeException>(() => LatticeDatabase.Open(path));
        Assert.Equal((int)NativeErrorCode.DatabaseLocked, writerConflict.NativeErrorCode);

        var readerConflict = Assert.Throws<LatticeException>(() =>
            LatticeDatabase.Open(path, new LatticeDatabaseOptions { ReadOnly = true }));
        Assert.Equal((int)NativeErrorCode.DatabaseLocked, readerConflict.NativeErrorCode);

        writer.Kill();
        using var reader = LatticeDatabase.Open(path, new LatticeDatabaseOptions { ReadOnly = true });
        using var secondReader = StartWorker("hold", path, "reader");

        var writerWhileReadersOpen = Assert.Throws<LatticeException>(() =>
            LatticeDatabase.Open(path));
        Assert.Equal((int)NativeErrorCode.DatabaseLocked, writerWhileReadersOpen.NativeErrorCode);

        secondReader.Kill();
    }

#if LATTICEDBSHARP_NATIVE_TESTS
    [Fact]
#else
    [Fact(Skip = "Enabled after the pinned LatticeDB native library is built and staged.")]
#endif
    public void Native_crash_recovery_preserves_committed_data_and_allows_new_writes()
    {
        using var directory = TemporaryDirectory.Create();
        var path = Path.Combine(directory.Path, "crash-recovery.ltdb");
        using var worker = StartWorker("crash", path);
        worker.Kill();

        using (var reopened = LatticeDatabase.Open(path))
        {
            using var committedQuery = reopened.Prepare("MATCH (n:Committed) RETURN n");
            using var uncommittedQuery = reopened.Prepare("MATCH (n:Uncommitted) RETURN n");
            Assert.Single(reopened.ExecuteRead(transaction => committedQuery.ExecuteAll(transaction)));
            Assert.Empty(reopened.ExecuteRead(transaction => uncommittedQuery.ExecuteAll(transaction)));

            reopened.ExecuteWrite(transaction => transaction.CreateNode("AfterCrash"));
        }

        using var final = LatticeDatabase.Open(path);
        using var query = final.Prepare("MATCH (n:AfterCrash) RETURN n");
        Assert.Single(final.ExecuteRead(transaction => query.ExecuteAll(transaction)));
    }

#if LATTICEDBSHARP_NATIVE_TESTS
    [Fact]
#else
    [Fact(Skip = "Enabled after the pinned LatticeDB native library is built and staged.")]
#endif
    public void Windows_file_paths_support_unicode_and_long_wal_suffixes()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var directory = TemporaryDirectory.Create();
        var segments = Enumerable.Range(0, 14)
            .Select(index => $"segment-{index:D2}-0123456789")
            .ToArray();
        var longDirectory = Path.Combine(new[] { directory.Path }.Concat(segments).ToArray());
        Directory.CreateDirectory(longDirectory);
        var path = Path.Combine(longDirectory, "graph-数据库-данные-😀.ltdb");
        Assert.True(path.Length > 260, $"Test path was only {path.Length} characters.");
        Assert.True((path + "-wal").Length > 260, "The WAL suffix must cross the long-path boundary too.");

        LatticeNodeId node;
        using (var database = LatticeDatabase.Open(path, new LatticeDatabaseOptions { Create = true }))
        using (var write = database.BeginWriteTransaction())
        {
            node = write.CreateNode("Persisted");
            write.Commit();
        }

        using var reopened = LatticeDatabase.Open(path);
        using var read = reopened.BeginReadTransaction();
        Assert.True(read.NodeExists(node));
        read.Commit();
    }

#if LATTICEDBSHARP_NATIVE_TESTS
    [Fact]
#else
    [Fact(Skip = "Enabled after the pinned LatticeDB native library is built and staged.")]
#endif
    public void Windows_file_read_only_open_rejects_writes_and_missing_paths()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var directory = TemporaryDirectory.Create();
        var path = Path.Combine(directory.Path, "read-only.ltdb");
        using (var database = LatticeDatabase.Open(path, new LatticeDatabaseOptions { Create = true }))
        using (var write = database.BeginWriteTransaction())
        {
            write.CreateNode("Existing");
            write.Commit();
        }

        using var readOnly = LatticeDatabase.Open(path, new LatticeDatabaseOptions { ReadOnly = true });
        using var read = readOnly.BeginReadTransaction();
        Assert.Throws<LatticeException>(() => readOnly.BeginWriteTransaction());
        read.Commit();

        var missing = Path.Combine(directory.Path, "missing.ltdb");
        var notFound = Assert.Throws<LatticeException>(() =>
            LatticeDatabase.Open(missing, new LatticeDatabaseOptions { ReadOnly = true }));
        Assert.Equal((int)NativeErrorCode.NotFound, notFound.NativeErrorCode);
        Assert.False(File.Exists(missing));
    }

#if LATTICEDBSHARP_NATIVE_TESTS
    [Fact]
#else
    [Fact(Skip = "Enabled after the pinned LatticeDB native library is built and staged.")]
#endif
    public void Edge_delete_properties_and_traversal_round_trip()
    {
        using var database = LatticeDatabase.OpenMemory();
        LatticeNodeId a;
        LatticeNodeId b;
        LatticeNodeId c;
        LatticeEdgeId knows;

        using (var write = database.BeginWriteTransaction())
        {
            a = write.CreateNode("Person");
            b = write.CreateNode("Person");
            c = write.CreateNode("Person");
            knows = write.CreateEdge(a, b, "KNOWS");
            write.CreateEdge(b, c, "KNOWS");
            write.CreateEdge(a, c, "LIKES");

            write.SetEdgeProperty(knows, "weight", LatticeValue.From(3L));
            Assert.True(write.TryGetEdgeProperty(knows, "weight", out var weight));
            Assert.Equal(LatticeValue.From(3L), weight);
            Assert.False(write.TryGetEdgeProperty(knows, "missing", out _));
            write.Commit();
        }

        using (var read = database.BeginReadTransaction())
        {
            var outgoing = read.GetOutgoingEdges(a);
            Assert.Equal(2, outgoing.Count);
            Assert.Contains(outgoing, edge => edge.Id == knows && edge.Type == "KNOWS" && edge.Source == a && edge.Target == b);
            var incoming = read.GetIncomingEdges(c);
            Assert.Equal(2, incoming.Count);
            var knowsOnly = read.GetOutgoingEdges(a, "KNOWS");
            Assert.Single(knowsOnly);
            var limited = read.GetOutgoingEdges(a, "KNOWS", limit: 1);
            Assert.Single(limited);
            read.Commit();
        }

        using (var write = database.BeginWriteTransaction())
        {
            write.RemoveEdgeProperty(knows, "weight");
            Assert.False(write.TryGetEdgeProperty(knows, "weight", out _));
            write.DeleteEdge(a, b, "KNOWS");
            Assert.Single(write.GetOutgoingEdges(a));
            Assert.Empty(write.GetOutgoingEdges(a, "KNOWS"));
            write.Commit();
        }

        using var verify = database.BeginReadTransaction();
        Assert.Empty(verify.GetIncomingEdges(b, "KNOWS"));
        verify.Commit();
    }

#if LATTICEDBSHARP_NATIVE_TESTS
    [Fact]
#else
    [Fact(Skip = "Enabled after the pinned LatticeDB native library is built and staged.")]
#endif
    public void Edge_operations_reject_read_only_disposed_and_malformed_input()
    {
        using var database = LatticeDatabase.OpenMemory();
        LatticeNodeId node;
        LatticeEdgeId edge;
        using (var write = database.BeginWriteTransaction())
        {
            node = write.CreateNode("Person");
            var other = write.CreateNode("Person");
            edge = write.CreateEdge(node, other, "KNOWS");
            Assert.Throws<ArgumentException>(() => write.DeleteEdge(node, other, "  "));
            Assert.Throws<ArgumentException>(() => write.SetEdgeProperty(edge, "", LatticeValue.From(1L)));
            Assert.Throws<ArgumentOutOfRangeException>(() => write.GetOutgoingEdges(node, "KNOWS", limit: -1));
            write.Commit();
        }

        using var read = database.BeginReadTransaction();
        Assert.Throws<InvalidOperationException>(() => read.DeleteEdge(node, node, "KNOWS"));
        Assert.Throws<InvalidOperationException>(() => read.SetEdgeProperty(edge, "k", LatticeValue.From(1L)));
        Assert.Throws<InvalidOperationException>(() => read.RemoveEdgeProperty(edge, "k"));
        Assert.Single(read.GetOutgoingEdges(node));
        read.Commit();

        var disposed = database.BeginReadTransaction();
        disposed.Dispose();
        Assert.Throws<ObjectDisposedException>(() => disposed.GetOutgoingEdges(node));
    }

#if LATTICEDBSHARP_NATIVE_TESTS
    [Fact]
#else
    [Fact(Skip = "Enabled after the pinned LatticeDB native library is built and staged.")]
#endif
    public void Concurrent_reads_on_one_transaction_stay_consistent()
    {
        using var database = LatticeDatabase.OpenMemory();
        LatticeNodeId hub;
        using (var write = database.BeginWriteTransaction())
        {
            hub = write.CreateNode("Hub");
            for (var index = 0; index < 10; index++)
            {
                var leaf = write.CreateNode("Leaf");
                write.CreateEdge(hub, leaf, "LINKS");
            }

            write.Commit();
        }

        using var read = database.BeginReadTransaction();
        var results = new System.Collections.Concurrent.ConcurrentBag<int>();
        System.Threading.Tasks.Parallel.For(0, 32, _ =>
        {
            results.Add(read.GetOutgoingEdges(hub).Count);
        });
        Assert.All(results, count => Assert.Equal(10, count));
        read.Commit();
    }

#if LATTICEDBSHARP_NATIVE_TESTS
    [Fact]
#else
    [Fact(Skip = "Enabled after the pinned LatticeDB native library is built and staged.")]
#endif
    public void Repeated_create_query_dispose_cycles_are_stable()
    {
        for (var cycle = 0; cycle < 25; cycle++)
        {
            using var database = LatticeDatabase.OpenMemory();
            LatticeNodeId first;
            using (var write = database.BeginWriteTransaction())
            {
                first = write.CreateNode("Cycle");
                var second = write.CreateNode("Cycle");
                var edge = write.CreateEdge(first, second, "LINKS");
                write.SetEdgeProperty(edge, "cycle", LatticeValue.From((long)cycle));
                write.Commit();
            }

            using var read = database.BeginReadTransaction();
            var edges = read.GetOutgoingEdges(first);
            Assert.Single(edges);
            Assert.True(read.TryGetEdgeProperty(edges[0].Id, "cycle", out var marker));
            Assert.Equal(LatticeValue.From((long)cycle), marker);
            read.Commit();
        }
    }

#if LATTICEDBSHARP_NATIVE_TESTS
    [Fact]
#else
    [Fact(Skip = "Enabled after the pinned LatticeDB native library is built and staged.")]
#endif
    public void Node_enumeration_lists_labeled_and_all_nodes()
    {
        using var database = LatticeDatabase.OpenMemory();
        LatticeNodeId first;
        LatticeNodeId second;
        using (var write = database.BeginWriteTransaction())
        {
            first = write.CreateNode("Person");
            second = write.CreateNode("Place");
            write.Commit();
        }

        using (var read = database.BeginReadTransaction())
        {
            Assert.Equal([first], read.GetNodesByLabel("Person"));
            Assert.Equal([second], read.GetNodesByLabel("Place"));
            Assert.Empty(read.GetNodesByLabel("Missing"));
            Assert.Equal(2, read.GetAllNodes().Count);
            read.Commit();
        }

        Assert.Equal([first], database.GetNodesByLabel("Person"));
        Assert.Empty(database.GetNodesByLabel("Missing"));
    }

#if LATTICEDBSHARP_NATIVE_TESTS
    [Fact]
#else
    [Fact(Skip = "Enabled after the pinned LatticeDB native library is built and staged.")]
#endif
    public void Database_serialize_deserialize_round_trip_is_independent()
    {
        using var database = LatticeDatabase.OpenMemory();
        LatticeNodeId node;
        using (var write = database.BeginWriteTransaction())
        {
            node = write.CreateNode("Persisted");
            write.SetProperty(node, "name", LatticeValue.From("Ada"));
            write.Commit();
        }

        var bytes = database.Serialize();
        Assert.NotEmpty(bytes);
        using var copy = LatticeDatabase.Deserialize(bytes);
        using (var read = copy.BeginReadTransaction())
        {
            Assert.True(read.NodeExists(node));
            Assert.True(read.TryGetProperty(node, "name", out var name));
            Assert.Equal("Ada", name.AsString());
            read.Commit();
        }

        using (var write = database.BeginWriteTransaction())
        {
            write.DeleteNode(node);
            write.Commit();
        }

        using var reread = copy.BeginReadTransaction();
        Assert.True(reread.NodeExists(node));
        reread.Commit();
    }

#if LATTICEDBSHARP_NATIVE_TESTS
    [Fact]
#else
    [Fact(Skip = "Enabled after the pinned LatticeDB native library is built and staged.")]
#endif
    public void Database_deserialize_rejects_corrupt_bytes()
    {
        Assert.Throws<LatticeException>(() => LatticeDatabase.Deserialize("not-a-database"u8.ToArray()));
        Assert.Throws<ArgumentException>(() => LatticeDatabase.Deserialize([]));
    }

#if LATTICEDBSHARP_NATIVE_TESTS
    [Fact]
#else
    [Fact(Skip = "Enabled after the pinned LatticeDB native library is built and staged.")]
#endif
    public void Property_indexes_serve_indexed_finds_and_drop_cleanly()
    {
        using var database = LatticeDatabase.OpenMemory();
        LatticeNodeId ada;
        LatticeNodeId bob;
        using (var write = database.BeginWriteTransaction())
        {
            ada = write.CreateNode("Person");
            write.SetProperty(ada, "name", LatticeValue.From("Ada"));
            bob = write.CreateNode("Person");
            write.SetProperty(bob, "name", LatticeValue.From("Bob"));
            var edge = write.CreateEdge(ada, bob, "KNOWS");
            write.SetEdgeProperty(edge, "weight", LatticeValue.From(3L));
            write.Commit();
        }

        database.CreateNodePropertyIndex("Person", "name");
        database.CreateEdgePropertyIndex("KNOWS", "weight");
        using (var read = database.BeginReadTransaction())
        {
            Assert.Equal([ada], read.FindNodesByLabelProperty("Person", "name", LatticeValue.From("Ada"), limit: 10));
            Assert.Empty(read.FindNodesByLabelProperty("Person", "name", LatticeValue.From("Nobody"), limit: 10));
            var limited = read.FindNodesByLabelProperty("Person", "name", LatticeValue.From("Ada"), limit: 1);
            Assert.Single(limited);
            var edges = read.FindEdgesByTypeProperty("KNOWS", "weight", LatticeValue.From(3L), limit: 10);
            Assert.Single(edges);
            Assert.Empty(read.FindEdgesByTypeProperty("KNOWS", "weight", LatticeValue.From(4L), limit: 10));
            read.Commit();
        }

        database.DropNodePropertyIndex("Person", "name");
        database.DropEdgePropertyIndex("KNOWS", "weight");
        using (var read = database.BeginReadTransaction())
        {
            Assert.Throws<LatticeException>(() => read.FindNodesByLabelProperty("Person", "name", LatticeValue.From("Ada"), limit: 10));
            Assert.Throws<LatticeException>(() => read.FindEdgesByTypeProperty("KNOWS", "weight", LatticeValue.From(3L), limit: 10));
            read.Commit();
        }
    }

#if LATTICEDBSHARP_NATIVE_TESTS
    [Fact]
#else
    [Fact(Skip = "Enabled after the pinned LatticeDB native library is built and staged.")]
#endif
    public void Vector_batch_insert_search_and_bind_round_trip()
    {
        using var database = LatticeDatabase.OpenMemory(new LatticeDatabaseOptions
        {
            EnableVector = true,
            VectorDimensions = 3,
        });
        IReadOnlyList<LatticeNodeId> ids;
        using (var write = database.BeginWriteTransaction())
        {
            ids = write.BatchInsertNodes(
            [
                new("Item", new float[] { 1, 0, 0 }),
                new("Item", new float[] { 0, 1, 0 }),
                new("Other", new float[] { 0, 0, 1 })
            ]);
            Assert.Equal(3, ids.Count);
            Assert.Empty(write.BatchInsertNodes([]));
            Assert.Throws<ArgumentException>(() => write.BatchInsertNodes([new("Item", Array.Empty<float>())]));
            Assert.Throws<ArgumentException>(() => write.BatchInsertNodes([new("Bad,Label", new float[] { 1, 0, 0 })]));
            write.Commit();
        }

        using (var read = database.BeginReadTransaction())
        {
            var hits = read.VectorSearch(new float[] { 1, 0, 0 }, count: 2);
            Assert.Equal(2, hits.Count);
            Assert.Equal(ids[0], hits[0].NodeId);
            Assert.True(hits[0].Distance <= hits[1].Distance);
            read.Commit();
        }

        var dbHits = database.VectorSearch(new float[] { 0, 0, 1 }, count: 1);
        Assert.Single(dbHits);
        Assert.Equal(3, database.VectorSearch(new float[] { 0, 0, 1 }, count: 10).Count);
        Assert.Throws<ArgumentOutOfRangeException>(() => database.VectorSearch(new float[] { 1, 0, 0 }, 0));
        Assert.Throws<ArgumentException>(() => database.VectorSearch(Array.Empty<float>(), 1));

        using var query = database.Prepare("MATCH (n) RETURN n LIMIT 1");
        query.BindVector("v", new float[] { 1, 2, 3 });
        Assert.Throws<ArgumentException>(() => query.BindVector("  ", new float[] { 1 }));
    }

#if LATTICEDBSHARP_NATIVE_TESTS
    [Fact]
#else
    [Fact(Skip = "Enabled after the pinned LatticeDB native library is built and staged.")]
#endif
    public void Full_text_search_scores_and_drops_cleanly()
    {
        using var database = LatticeDatabase.OpenMemory();
        Assert.False(database.NodeFtsIndexExists("Article", "text"));
        using (var write = database.BeginWriteTransaction())
        {
            var first = write.CreateNode("Article");
            write.SetProperty(first, "text", LatticeValue.From("The quick brown fox jumps."));
            var second = write.CreateNode("Article");
            write.SetProperty(second, "text", LatticeValue.From("A slow green turtle walks."));
            var other = write.CreateNode("Note");
            write.SetProperty(other, "text", LatticeValue.From("The quick brown fox jumps."));
            write.Commit();
        }

        Assert.Throws<LatticeException>(() => database.FtsSearch("Article", "text", "quick", limit: 10));
        database.CreateNodeFtsIndex("Article", "text");
        Assert.True(database.NodeFtsIndexExists("Article", "text"));

        using (var read = database.BeginReadTransaction())
        {
            var hits = read.FtsSearch("Article", "text", "quick fox", limit: 10);
            Assert.Single(hits);
            var fuzzy = read.FtsSearchFuzzy("Article", "text", "quikc", limit: 10);
            Assert.Single(fuzzy);
            Assert.Equal(hits[0].NodeId, fuzzy[0].NodeId);
            Assert.True(hits[0].Score > 0);
            read.Commit();
        }

        var dbHits = database.FtsSearch("Article", "text", "turtle", limit: 10);
        Assert.Single(dbHits);
        database.DropNodeFtsIndex("Article", "text");
        Assert.False(database.NodeFtsIndexExists("Article", "text"));
        Assert.Throws<LatticeException>(() => database.FtsSearch("Article", "text", "quick", limit: 10));
    }

#if LATTICEDBSHARP_NATIVE_TESTS
    [Fact]
#else
    [Fact(Skip = "Enabled after the pinned LatticeDB native library is built and staged.")]
#endif
    public void Identifiers_reject_embedded_nulls_before_index_or_fts_operations()
    {
        using var database = LatticeDatabase.OpenMemory();
        const string invalidLabel = "Article\0Unexpected";
        const string invalidProperty = "text\0Unexpected";
        const string invalidUtf16 = "Article\uD800";

        Assert.Throws<ArgumentException>(() => database.CreateNodePropertyIndex(invalidLabel, "text"));
        Assert.Throws<ArgumentException>(() => database.DropNodePropertyIndex("Article", invalidProperty));
        Assert.Throws<ArgumentException>(() => database.CreateNodeFtsIndex(invalidLabel, "text"));
        Assert.Throws<ArgumentException>(() => database.DropNodeFtsIndex("Article", invalidProperty));
        Assert.Throws<ArgumentException>(() => database.NodeFtsIndexExists(invalidLabel, "text"));
        Assert.Throws<ArgumentException>(() => database.FtsSearch(invalidLabel, "text", "query", 10));
        Assert.Throws<ArgumentException>(() => database.FtsSearch("Article", invalidProperty, "query", 10));
        Assert.Throws<ArgumentException>(() => database.CreateEdgePropertyIndex("REL\0Unexpected", "weight"));
        Assert.Throws<ArgumentException>(() => database.CreateEdgeFtsIndex("REL\0Unexpected", "text"));
        Assert.Throws<ArgumentException>(() => database.CreateNodeFtsIndex(invalidUtf16, "text"));

        using (var write = database.BeginWriteTransaction())
        {
            var article = write.CreateNode("Article");
            write.SetProperty(article, "text", LatticeValue.From("safe index"));
            write.Commit();
        }

        database.CreateNodeFtsIndex("Article", "text");
        Assert.Throws<ArgumentException>(() => database.DropNodeFtsIndex(invalidLabel, "text"));
        Assert.True(database.NodeFtsIndexExists("Article", "text"));
        database.DropNodeFtsIndex("Article", "text");
    }

#if LATTICEDBSHARP_NATIVE_TESTS
    [Fact]
#else
    [Fact(Skip = "Enabled after the pinned LatticeDB native library is built and staged.")]
#endif
    public void Failed_batch_requires_rollback_and_reports_native_progress()
    {
        using var database = LatticeDatabase.OpenMemory(new LatticeDatabaseOptions
        {
            EnableVector = true,
            VectorDimensions = 2,
        });
        using var write = database.BeginWriteTransaction();

        var exception = Assert.Throws<LatticeBatchInsertException>(() => write.BatchInsertNodes(
        [
            new("Valid", new float[] { 1, 0 }),
            new("Invalid", new float[] { 1, 0, 0 }),
        ]));

        Assert.Equal(2, exception.RequestedCount);
        Assert.InRange(exception.CompletedCount, 0, exception.RequestedCount);
        Assert.True(write.RequiresRollback);
        Assert.Throws<InvalidOperationException>(write.Commit);
        Assert.Throws<InvalidOperationException>(() => write.CreateNode("TooLate"));
        write.Rollback();
        Assert.True(write.IsCompleted);
    }

#if LATTICEDBSHARP_NATIVE_TESTS
    [Fact]
#else
    [Fact(Skip = "Enabled after the pinned LatticeDB native library is built and staged.")]
#endif
    public void Hash_embeddings_are_deterministic_lexical_fingerprints()
    {
        var first = LatticeHashEmbeddings.HashEmbed("The quick brown fox", 64);
        var second = LatticeHashEmbeddings.HashEmbed("The quick brown fox", 64);
        Assert.Equal(64, first.Length);
        Assert.Equal(first, second);
        Assert.NotEqual(first, LatticeHashEmbeddings.HashEmbed("Completely different words here", 64));
        Assert.Equal(128, LatticeHashEmbeddings.HashEmbed("The quick brown fox", 128).Length);
        Assert.Throws<ArgumentException>(() => LatticeHashEmbeddings.HashEmbed("  ", 64));
        Assert.Throws<ArgumentOutOfRangeException>(() => LatticeHashEmbeddings.HashEmbed("text", 0));

        using var database = LatticeDatabase.OpenMemory(new LatticeDatabaseOptions
        {
            EnableVector = true,
            VectorDimensions = 64,
        });
        using (var write = database.BeginWriteTransaction())
        {
            var node = write.CreateNode("Doc");
            write.SetVector(node, first);
            write.Commit();
        }

        using var read = database.BeginReadTransaction();
        var hits = read.VectorSearch(first, count: 1);
        Assert.Single(hits);
        read.Commit();
    }

#if LATTICEDBSHARP_NATIVE_TESTS
    [Fact]
#else
    [Fact(Skip = "Enabled after the pinned LatticeDB native library is built and staged.")]
#endif
    // A single flipped byte can land in unchecked space and open normally;
    // widespread corruption across the file must fail at open instead.
    public void Corrupt_database_file_fails_cleanly_without_harming_others()
    {
        using var directory = TemporaryDirectory.Create();
        var path = Path.Combine(directory.Path, "corrupt.ltdb");
        using (var database = LatticeDatabase.Open(path, new LatticeDatabaseOptions { Create = true }))
        using (var write = database.BeginWriteTransaction())
        {
            var node = write.CreateNode("Persisted");
            write.SetProperty(node, "name", LatticeValue.From("Ada"));
            write.Commit();
        }

        var bytes = File.ReadAllBytes(path);
        for (var index = 0; index < bytes.Length; index += 511)
        {
            bytes[index] ^= 0xff;
        }

        File.WriteAllBytes(path, bytes);
        Assert.Throws<LatticeException>(() => LatticeDatabase.Open(path));

        var healthyPath = Path.Combine(directory.Path, "healthy.ltdb");
        using var healthy = LatticeDatabase.Open(healthyPath, new LatticeDatabaseOptions { Create = true });
        using var verify = healthy.BeginWriteTransaction();
        verify.CreateNode("Alive");
        verify.Commit();
    }

#if LATTICEDBSHARP_NATIVE_TESTS
    [Fact]
#else
    [Fact(Skip = "Enabled after the pinned LatticeDB native library is built and staged.")]
#endif
    public void Truncated_tail_repairs_or_rejects_within_process_deadline()
    {
        using var directory = TemporaryDirectory.Create();
        var path = Path.Combine(directory.Path, "truncated.ltdb");
        using (var database = LatticeDatabase.Open(path, new LatticeDatabaseOptions { Create = true }))
        using (var write = database.BeginWriteTransaction())
        {
            write.CreateNode("Persisted");
            write.Commit();
        }

        var bytes = File.ReadAllBytes(path);
        File.WriteAllBytes(path, bytes[..Math.Max(0, bytes.Length - 8)]);
        var probe = RunWorkerToCompletion("probe-open", path, TimeSpan.FromSeconds(15));
        Assert.True(probe.ExitCode == 0, probe.StandardError);
    }

#if LATTICEDBSHARP_NATIVE_TESTS
    [Fact]
#else
    [Fact(Skip = "Enabled after the pinned LatticeDB native library is built and staged.")]
#endif
    public void Full_database_serialize_restore_preserves_graph()
    {
        using var database = LatticeDatabase.OpenMemory();
        using (var write = database.BeginWriteTransaction())
        {
            LatticeNodeId? previous = null;
            for (var index = 0; index < 100; index++)
            {
                var node = write.CreateNode("Chain");
                write.SetProperty(node, "index", LatticeValue.From((long)index));
                if (previous is { } parent)
                {
                    write.CreateEdge(parent, node, "LINKS");
                }

                previous = node;
            }

            write.Commit();
        }

        var restored = LatticeDatabase.Deserialize(database.Serialize());
        using var read = restored.BeginReadTransaction();
        var nodes = read.GetAllNodes();
        Assert.Equal(100, nodes.Count);
        var totalEdges = 0;
        foreach (var node in nodes)
        {
            totalEdges += read.GetOutgoingEdges(node).Count;
        }

        Assert.Equal(99, totalEdges);
        read.Commit();
        restored.Dispose();
    }

#if LATTICEDBSHARP_NATIVE_TESTS
    [Fact]
#else
    [Fact(Skip = "Enabled after the pinned LatticeDB native library is built and staged.")]
#endif
    public async Task Second_open_of_locked_file_fails_fast_instead_of_hanging()
    {
        using var directory = TemporaryDirectory.Create();
        var path = Path.Combine(directory.Path, "locked.ltdb");
        using var first = LatticeDatabase.Open(path, new LatticeDatabaseOptions { Create = true });
        var second = Task.Run(() =>
        {
            try
            {
                using var database = LatticeDatabase.Open(path);
                return "opened";
            }
            catch (LatticeException)
            {
                return "rejected";
            }
        });
        var completed = await Task.WhenAny(second, Task.Delay(TimeSpan.FromSeconds(15)));
        Assert.Same(second, completed);
        Assert.Equal("rejected", await second);
    }

#if LATTICEDBSHARP_NATIVE_TESTS
    [Fact]
#else
    [Fact(Skip = "Enabled after the pinned LatticeDB native library is built and staged.")]
#endif
    public void Repeated_database_lifecycles_stay_memory_bounded()
    {
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        var baseline = GC.GetTotalMemory(forceFullCollection: true);
        for (var cycle = 0; cycle < 100; cycle++)
        {
            using var database = LatticeDatabase.OpenMemory();
            using (var write = database.BeginWriteTransaction())
            {
                var first = write.CreateNode("Cycle");
                var second = write.CreateNode("Cycle");
                write.CreateEdge(first, second, "LINKS");
                write.Commit();
            }

            using var read = database.BeginReadTransaction();
            Assert.Equal(2, read.GetAllNodes().Count);
            read.Commit();
        }

        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        var growth = GC.GetTotalMemory(forceFullCollection: false) - baseline;
        Assert.True(growth < 64L * 1024 * 1024, $"Managed memory grew by {growth} bytes over 100 lifecycles.");
    }

#if LATTICEDBSHARP_NATIVE_TESTS
    [Fact]
#else
    [Fact(Skip = "Enabled after the pinned LatticeDB native library is built and staged.")]
#endif
    public void Repeated_database_lifecycles_keep_process_memory_bounded()
    {
        var probe = RunWorkerToCompletion("memory", "unused", TimeSpan.FromSeconds(30));
        Assert.True(probe.ExitCode == 0, probe.StandardError);
        var samples = probe.StandardOutput.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, samples.Length);
        var baseline = long.Parse(samples[0], System.Globalization.CultureInfo.InvariantCulture);
        var final = long.Parse(samples[1], System.Globalization.CultureInfo.InvariantCulture);
        var growth = final - baseline;
        Assert.True(growth < 128L * 1024 * 1024,
            $"Process working set grew by {growth} bytes over 100 native lifecycles.");
    }

    private static WorkerProbeResult RunWorkerToCompletion(string mode, string path, TimeSpan timeout)
    {
        var workerAssembly = typeof(WorkerMarker).Assembly.Location;
        var dotnet = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? Environment.ProcessPath;
        Assert.False(string.IsNullOrWhiteSpace(dotnet));
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo(dotnet!)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WorkingDirectory = AppContext.BaseDirectory,
            },
        };
        process.StartInfo.ArgumentList.Add(workerAssembly);
        process.StartInfo.ArgumentList.Add(mode);
        process.StartInfo.ArgumentList.Add(path);
        Assert.True(process.Start(), "Unable to start the native worker probe.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(checked((int)timeout.TotalMilliseconds)))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit(15_000);
            throw new TimeoutException($"Native worker probe '{mode}' exceeded {timeout}.");
        }

        return new WorkerProbeResult(
            process.ExitCode,
            output.GetAwaiter().GetResult(),
            error.GetAwaiter().GetResult());
    }

    private static WorkerProcess StartWorker(string mode, string path, string? access = null)
    {
        var workerAssembly = typeof(WorkerMarker).Assembly.Location;
        var dotnet = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? Environment.ProcessPath;
        Assert.False(string.IsNullOrWhiteSpace(dotnet));

        var process = new Process
        {
            StartInfo = new ProcessStartInfo(dotnet!)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                WorkingDirectory = AppContext.BaseDirectory,
            },
        };
        process.StartInfo.ArgumentList.Add(workerAssembly);
        process.StartInfo.ArgumentList.Add(mode);
        process.StartInfo.ArgumentList.Add(path);
        if (access is not null)
        {
            process.StartInfo.ArgumentList.Add(access);
        }

        Assert.True(process.Start(), "Unable to start the native worker process.");
        string? ready;
        try
        {
            ready = process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(15)).GetAwaiter().GetResult();
        }
        catch
        {
            TerminateAndDispose(process);
            throw;
        }
        if (ready != "READY")
        {
            string error;
            try
            {
                error = process.StandardError.ReadToEndAsync().WaitAsync(TimeSpan.FromSeconds(2)).GetAwaiter().GetResult();
            }
            catch (TimeoutException)
            {
                error = "<worker did not close stderr>";
            }
            TerminateAndDispose(process);
            throw new InvalidOperationException($"Native worker did not become ready. Output='{ready}', error='{error}'.");
        }

        return new WorkerProcess(process);
    }

    private static void TerminateAndDispose(Process process)
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit(15_000);
        }

        process.Dispose();
    }

    private sealed class WorkerProcess : IDisposable
    {
        private readonly Process process;

        internal WorkerProcess(Process process)
        {
            this.process = process;
        }

        internal void Kill()
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                Assert.True(process.WaitForExit(15_000), "Native worker did not exit after termination.");
            }
        }

        public void Dispose()
        {
            if (!process.HasExited)
            {
                try
                {
                    process.StandardInput.Close();
                    if (!process.WaitForExit(5_000))
                    {
                        process.Kill(entireProcessTree: true);
                        process.WaitForExit(15_000);
                    }
                }
                catch (InvalidOperationException)
                {
                    // The worker may have exited while the test was asserting its behavior.
                }
            }

            process.Dispose();
        }
    }

    private sealed record WorkerProbeResult(int ExitCode, string StandardOutput, string StandardError);

    private sealed class TemporaryDirectory : IDisposable
    {
        private TemporaryDirectory(string path)
        {
            Path = path;
            Directory.CreateDirectory(path);
        }

        internal string Path { get; }

        internal static TemporaryDirectory Create()
        {
            return new TemporaryDirectory(System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"latticedbsharp-{Guid.NewGuid():N}"));
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference AbandonWriteTransaction(LatticeDatabase database)
    {
        var transaction = database.BeginWriteTransaction();
        transaction.CreateNode("Abandoned");
        return new WeakReference(transaction);
    }

    private static void AssertStruct<T>(JsonElement native, params (string JsonName, string ManagedField)[] fields)
        where T : struct
    {
        Assert.Equal(Marshal.SizeOf<T>(), native.GetProperty("size").GetInt32());
        foreach (var field in fields)
        {
            AssertOffset<T>(native, field.JsonName, field.ManagedField);
        }
    }

    private static void AssertOffset<T>(JsonElement nativeOptions, string jsonName, string managedField)
        where T : struct
    {
        Assert.Equal(
            Marshal.OffsetOf<T>(managedField).ToInt32(),
            nativeOptions.GetProperty(jsonName).GetInt32());
    }
}
