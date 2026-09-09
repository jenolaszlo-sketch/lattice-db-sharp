using LatticeDbSharp;

using var database = LatticeDatabase.OpenMemory();
LatticeNodeId alice;
LatticeNodeId bob;

using (var write = database.BeginWriteTransaction())
{
    alice = write.CreateNode("Person");
    bob = write.CreateNode("Person");
    _ = write.CreateEdge(alice, bob, "KNOWS");
    write.Commit();
}

using var read = database.BeginReadTransaction();
Console.WriteLine($"Alice exists: {read.NodeExists(alice)}");
Console.WriteLine($"Bob exists: {read.NodeExists(bob)}");
read.Commit();
