// NativeAOT smoke test: exercises the binding surface that must survive
// trimming and ahead-of-time compilation. Any failure throws, so a zero exit
// code means the AOT-published binary round-tripped the engine.
using LatticeDbSharp;

static void Check(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException($"AOT smoke check failed: {message}");
    }
}

Check(LatticeNative.IsAvailable, "native library is available");
Check(!string.IsNullOrWhiteSpace(LatticeNative.Version), "native version is reported");

using var database = LatticeDatabase.OpenMemory();
LatticeNodeId alice;
LatticeNodeId bob;
LatticeEdgeId edge;
using (var write = database.BeginWriteTransaction())
{
    alice = write.CreateNode("Person");
    bob = write.CreateNode("Person");
    edge = write.CreateEdge(alice, bob, "KNOWS");
    write.SetProperty(alice, "name", LatticeValue.From("Ada"));
    write.SetEdgeProperty(edge, "weight", LatticeValue.From(3L));
    write.Commit();
}

using (var read = database.BeginReadTransaction())
{
    Check(read.NodeExists(alice), "committed node is visible");
    Check(read.TryGetProperty(alice, "name", out var name) && name.AsString() == "Ada", "node property round-trips");
    Check(read.TryGetEdgeProperty(edge, "weight", out var weight) && weight.AsInt64() == 3L, "edge property round-trips");
    var outgoing = read.GetOutgoingEdges(alice);
    Check(outgoing.Count == 1 && outgoing[0].Id == edge && outgoing[0].Type == "KNOWS", "outgoing traversal finds the edge");
    Check(read.GetIncomingEdges(bob).Count == 1, "incoming traversal finds the edge");
    read.Commit();
}

using (var query = database.Prepare("MATCH (n:Person) WHERE n.name = $name RETURN n"))
{
    query.Bind("name", LatticeValue.From("Ada"));
    using var write = database.BeginWriteTransaction();
    using var result = query.Execute(write);
    var rows = result.ReadAll();
    Check(rows.Count == 1, "parameterized query returns one row");
    write.Commit();
}

Console.WriteLine("LatticeDbSharp AOT smoke test passed.");
