using LatticeDbSharp;

namespace LatticeDbSharp.NativeWorker;

public static class WorkerMarker
{
}

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length is < 2 or > 3)
        {
            Console.Error.WriteLine("Expected '<hold|crash|resolver|probe-open|memory> <database-path>'.");
            return 2;
        }

        try
        {
            return args[0] switch
            {
                "hold" => Hold(args[1], args.Length == 3 && args[2] == "reader"),
                "crash" => Crash(args[1]),
                "resolver" => Resolver(args[1]),
                "probe-open" => ProbeOpen(args[1]),
                "memory" => MemoryProbe(),
                _ => 2,
            };
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static int Hold(string path, bool readOnly)
    {
        using var database = LatticeDatabase.Open(
            path,
            new LatticeDatabaseOptions { Create = !readOnly, ReadOnly = readOnly });
        Ready();
        _ = Console.ReadLine();
        return 0;
    }

    private static int Crash(string path)
    {
        using var database = LatticeDatabase.Open(
            path,
            new LatticeDatabaseOptions { Create = true });

        using (var committed = database.BeginWriteTransaction())
        {
            committed.CreateNode("Committed");
            committed.Commit();
        }

        using var uncommitted = database.BeginWriteTransaction();
        uncommitted.CreateNode("Uncommitted");
        Ready();
        _ = Console.ReadLine();
        return 0;
    }

    private static int Resolver(string path)
    {
        using var database = LatticeDatabase.Open(path, new LatticeDatabaseOptions { Create = true });
        const string variable = "LATTICEDBSHARP_NATIVE_LIBRARY";
        var original = Environment.GetEnvironmentVariable(variable);
        try
        {
            Environment.SetEnvironmentVariable(variable, Path.Combine(path, "missing-lattice.dll"));
            database.CreateEdgePropertyIndex("REL", "weight");
            Ready();
            _ = Console.ReadLine();
            return 0;
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, original);
        }
    }

    private static int ProbeOpen(string path)
    {
        try
        {
            using var database = LatticeDatabase.Open(path);
            using var read = database.BeginReadTransaction();
            read.Commit();
        }
        catch (LatticeException)
        {
            // A truncated file may be rejected. The process boundary and
            // caller deadline prove that either supported outcome terminates.
        }

        return 0;
    }

    private static int MemoryProbe()
    {
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        using var process = System.Diagnostics.Process.GetCurrentProcess();
        process.Refresh();
        var baseline = process.WorkingSet64;
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
            _ = read.GetAllNodes();
            read.Commit();
        }

        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        process.Refresh();
        Console.WriteLine($"{baseline} {process.WorkingSet64}");
        return 0;
    }

    private static void Ready()
    {
        Console.WriteLine("READY");
        Console.Out.Flush();
    }
}
