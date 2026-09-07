using LatticeDBSharp.Interop;
using Xunit;

namespace LatticeDBSharp.Tests;

public sealed class ScaffoldTests
{
    [Fact]
    public void Managed_binding_assembly_has_the_expected_identity()
    {
        Assert.Equal("LatticeDBSharp", typeof(InteropAssemblyMarker).Assembly.GetName().Name);
    }
}
