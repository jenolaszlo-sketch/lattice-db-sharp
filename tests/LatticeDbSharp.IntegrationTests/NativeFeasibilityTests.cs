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
            writeQuery.Bind("name", LatticeValue.From("committed"));
            using (writeQuery.Execute(committed))
            {
            }

            committed.Commit();
        }

        using (var rolledBack = database.BeginWriteTransaction())
        {
            writeQuery.Bind("name", LatticeValue.From("rolled-back"));
            using (writeQuery.Execute(rolledBack))
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
        var ready = process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(15)).GetAwaiter().GetResult();
        if (ready != "READY")
        {
            var error = process.StandardError.ReadToEndAsync().WaitAsync(TimeSpan.FromSeconds(2)).GetAwaiter().GetResult();
            process.Dispose();
            throw new InvalidOperationException($"Native worker did not become ready. Output='{ready}', error='{error}'.");
        }

        return new WorkerProcess(process);
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
