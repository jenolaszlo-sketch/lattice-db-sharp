using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using LatticeDbSharp;

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);

[MemoryDiagnoser]
public class RetrievalBenchmarks
{
    private LatticeDatabase _database = null!;
    private LatticeNodeId _seed;
    private float[] _query = null!;

    [Params(100, 1000)]
    public int NodeCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _database = LatticeDatabase.OpenMemory(new LatticeDatabaseOptions
        {
            EnableVector = true,
            VectorDimensions = 8
        });
        using (var write = _database.BeginWriteTransaction())
        {
            LatticeNodeId? previous = null;
            for (var index = 0; index < NodeCount; index++)
            {
                var node = write.CreateNode("Item");
                write.SetProperty(node, "name", LatticeValue.From($"item-{index:D5}"));
                var vector = new float[8];
                vector[index % 8] = 1;
                write.SetVector(node, vector);
                if (previous is { } parent)
                {
                    write.CreateEdge(parent, node, "LINKS");
                }

                previous = node;
            }

            _seed = previous!.Value;
            write.Commit();
        }

        _database.CreateNodePropertyIndex("Item", "name");
        _database.CreateNodeFtsIndex("Item", "name");
        _query = [1, 0, 0, 0, 0, 0, 0, 0];
    }

    [GlobalCleanup]
    public void Cleanup() => _database.Dispose();

    [Benchmark]
    public int IndexedNodeFind() =>
        _database.ExecuteRead(transaction =>
            transaction.FindNodesByLabelProperty("Item", "name", LatticeValue.From("item-00007"), limit: 10).Count);

    [Benchmark]
    public int VectorSearchTop10() =>
        _database.ExecuteRead(transaction =>
            transaction.VectorSearch(_query, count: 10).Count);

    [Benchmark]
    public int FtsSearch() =>
        _database.ExecuteRead(transaction =>
            transaction.FtsSearch("Item", "name", "item-00007", limit: 10).Count);

    [Benchmark]
    public int EdgeTraversal() =>
        _database.ExecuteRead(transaction =>
            transaction.GetIncomingEdges(_seed).Count);

    [Benchmark]
    public int CypherExecuteAll()
    {
        using var query = _database.Prepare("MATCH (n:Item) WHERE n.name = $name RETURN n");
        query.Bind("name", LatticeValue.From("item-00007"));
        return _database.ExecuteRead(transaction => query.ExecuteAll(transaction).Count);
    }
}
