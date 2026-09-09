using LatticeDBSharp;

namespace LatticeDBSharp.NativeWorker;

public static class WorkerMarker
{
}

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length is < 2 or > 3)
        {
            Console.Error.WriteLine("Expected '<hold|crash> <database-path>'.");
            return 2;
        }

        try
        {
            return args[0] switch
            {
                "hold" => Hold(args[1], args.Length == 3 && args[2] == "reader"),
                "crash" => Crash(args[1]),
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

    private static void Ready()
    {
        Console.WriteLine("READY");
        Console.Out.Flush();
    }
}
