using Xunit;

namespace LatticeDBSharp.IntegrationTests;

public sealed class NativeFeasibilityTests
{
    [Fact(Skip = "Enabled after the pinned LatticeDB native library is built and staged.")]
    public void Pinned_native_library_opens_and_reports_its_version()
    {
    }
}
