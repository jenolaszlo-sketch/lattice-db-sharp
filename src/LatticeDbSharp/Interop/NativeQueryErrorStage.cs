namespace LatticeDbSharp.Interop;

internal enum NativeQueryErrorStage
{
    None = 0,
    Parse = 1,
    Semantic = 2,
    Plan = 3,
    Execution = 4,
}
