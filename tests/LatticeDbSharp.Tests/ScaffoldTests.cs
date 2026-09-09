using System.Reflection;
using System.Runtime.InteropServices;
using LatticeDbSharp.Interop;
using Xunit;

namespace LatticeDbSharp.Tests;

public sealed class ScaffoldTests
{
    [Fact]
    public void Managed_binding_assembly_has_the_expected_identity()
    {
        Assert.Equal("LatticeDbSharp", typeof(InteropAssemblyMarker).Assembly.GetName().Name);
    }

    [Fact]
    public void Managed_binding_assembly_identifies_the_pinned_native_contract()
    {
        var metadata = typeof(InteropAssemblyMarker).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .ToDictionary(attribute => attribute.Key, attribute => attribute.Value);

        Assert.Equal("0.15.0", metadata["LatticeDB.NativeVersion"]);
        Assert.Equal("9800159e22e200f2b6888c7c6be1810adb695506", metadata["LatticeDB.NativeCommit"]);
    }

    [Fact]
    public void Native_diagnostics_expose_the_same_pinned_contract()
    {
        Assert.Equal("0.15.0", LatticeNative.PinnedVersion);
        Assert.Equal("9800159e22e200f2b6888c7c6be1810adb695506", LatticeNative.PinnedCommit);
    }

    [Fact]
    public void Missing_explicit_native_library_has_actionable_diagnostics()
    {
        const string variable = "LATTICEDBSHARP_NATIVE_LIBRARY";
        var original = Environment.GetEnvironmentVariable(variable);
        var missingPath = Path.GetFullPath(Path.Combine(
            Path.GetTempPath(),
            $"latticedbsharp-missing-{Guid.NewGuid():N}",
            OperatingSystem.IsWindows() ? "lattice.dll" : "liblattice.so"));

        try
        {
            Environment.SetEnvironmentVariable(variable, missingPath);

            Assert.False(LatticeNative.IsAvailable);
            var error = Assert.Throws<LatticeNativeLoadException>(() => _ = LatticeNative.Version);
            Assert.Contains(missingPath, error.ToString(), StringComparison.Ordinal);
            Assert.Contains(LatticeNative.PinnedVersion, error.Message, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, original);
        }
    }

    [Fact]
    public void Value_strings_reject_unpaired_utf16_surrogates()
    {
        var invalid = "bad\uD800";

        Assert.Throws<ArgumentException>(() => LatticeValue.From(invalid));
        Assert.Throws<ArgumentException>(() => LatticeValue.From(
            new Dictionary<string, LatticeValue> { [invalid] = LatticeValue.Null }));
        Assert.Throws<ArgumentException>(() => LatticeDatabase.Open(invalid));
    }

    [Fact]
    public void Byte_and_vector_accessors_do_not_expose_backing_storage()
    {
        var bytes = new byte[] { 1, 2 };
        var vector = new float[] { 1, 2 };
        var byteValue = LatticeValue.From(bytes);
        var vectorValue = LatticeValue.From((ReadOnlyMemory<float>)vector);

        bytes[0] = 9;
        vector[0] = 9;

        Assert.Equal(new byte[] { 1, 2 }, byteValue.AsBytes().ToArray());
        Assert.Equal(new float[] { 1, 2 }, vectorValue.AsVector().ToArray());

        var firstRead = byteValue.AsBytes().ToArray();
        firstRead[0] = 8;
        Assert.Equal(1, byteValue.AsBytes().Span[0]);
    }

    [Fact]
    public void Recursive_value_builder_enforces_depth_and_allocation_limits()
    {
        var nested = LatticeValue.Null;
        for (var index = 0; index <= NativeValueLimits.MaxDepth; index++)
        {
            nested = LatticeValue.From(new[] { nested });
        }

        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            using var builder = new NativeValueBuilder(nested);
        });

        var oversized = LatticeValue.From(new byte[checked((int)NativeValueLimits.MaxCopiedBytes + 1)]);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            using var builder = new NativeValueBuilder(oversized);
        });
    }

    [Fact]
    public void Recursive_value_decoder_rejects_oversized_native_lengths_before_dereferencing()
    {
        var listPointer = Marshal.AllocHGlobal(Marshal.SizeOf<NativeListValue>());
        try
        {
            Marshal.StructureToPtr(
                new NativeListValue
                {
                    Items = nint.Zero,
                    Length = (nuint)(NativeValueLimits.MaxCollectionItems + 1),
                },
                listPointer,
                fDeleteOld: false);

            Assert.Throws<InvalidDataException>(() =>
            {
                _ = NativeValueConversion.ToManaged(new NativeValue
                {
                    Type = NativeValueType.List,
                    ListValue = listPointer,
                });
            });
        }
        finally
        {
            Marshal.FreeHGlobal(listPointer);
        }

        Assert.Throws<InvalidDataException>(() =>
        {
            _ = NativeValueConversion.ToManaged(new NativeValue
            {
                Type = NativeValueType.Bytes,
                BytesValue = new NativeStringValue
                {
                    Length = (nuint)(NativeValueLimits.MaxCopiedBytes + 1),
                },
            });
        });
    }

    [Fact]
    public void Native_recursive_pointer_overflow_is_reported_as_invalid_data()
    {
        Assert.Throws<InvalidDataException>(() =>
            _ = NativeValueConversion.AddOffset(nint.MaxValue, 1, 1));
    }

    [Fact]
    public void Native_label_decoder_rejects_a_successful_null_output()
    {
        Assert.Throws<InvalidDataException>(() =>
            _ = LatticeTransaction.DecodeLabels(nint.Zero));
    }

    [Fact]
    public void Rows_support_ordinal_access_without_losing_name_lookup()
    {
        var row = new LatticeRow(
            new[] { "first", "second" },
            new[] { LatticeValue.From(1L), LatticeValue.From("two") });

        Assert.Equal(1L, row[0].AsInt64());
        Assert.Equal("two", row.GetValue(1).AsString());
        Assert.True(row.TryGetOrdinal("second", out var ordinal));
        Assert.Equal(1, ordinal);
    }
}
