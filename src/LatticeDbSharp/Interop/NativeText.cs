using System.Runtime.InteropServices;
using System.Text;

namespace LatticeDbSharp.Interop;

internal static class NativeText
{
    internal static readonly Encoding Utf8 = new UTF8Encoding(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    internal static void Validate(string value, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value);
        try
        {
            _ = Utf8.GetByteCount(value);
        }
        catch (EncoderFallbackException exception)
        {
            throw new ArgumentException(
                "The value contains an unpaired UTF-16 surrogate and cannot be encoded as UTF-8.",
                parameterName,
                exception);
        }
    }

    internal static int GetByteCount(string value, string parameterName)
    {
        Validate(value, parameterName);
        return Utf8.GetByteCount(value);
    }

    internal static byte[] Encode(string value, string parameterName)
    {
        Validate(value, parameterName);
        return Utf8.GetBytes(value);
    }

    internal static string? DecodeNullTerminated(nint pointer)
    {
        if (pointer == nint.Zero)
        {
            return null;
        }

        var length = 0;
        while (length < NativeValueLimits.MaxNativeTextBytes && Marshal.ReadByte(pointer, length) != 0)
        {
            length++;
        }

        if (length == NativeValueLimits.MaxNativeTextBytes)
        {
            throw new InvalidDataException("Native UTF-8 text exceeded the managed safety limit.");
        }

        if (length == 0)
        {
            return string.Empty;
        }

        var bytes = new byte[length];
        Marshal.Copy(pointer, bytes, 0, length);
        try
        {
            return Utf8.GetString(bytes);
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException("Native text contained invalid UTF-8.", exception);
        }
    }
}
