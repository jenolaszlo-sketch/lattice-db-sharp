using LatticeDBSharp;

using var database = LatticeDatabase.OpenMemory();
LatticeNodeId node;
using (var write = database.BeginWriteTransaction())
{
    node = write.CreateNode("PackageSmoke");
    write.Commit();
}

using (var read = database.BeginReadTransaction())
{
    if (!read.NodeExists(node))
    {
        throw new InvalidOperationException("The committed package-smoke node was not visible.");
    }

    read.Commit();
}

Console.WriteLine("LatticeDBSharp package smoke test passed.");
