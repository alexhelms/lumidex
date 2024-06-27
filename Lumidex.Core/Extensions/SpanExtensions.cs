using System.Runtime.InteropServices;
using System.Text;

namespace System;

public static class SpanExtensions
{
    public unsafe static string NullTerminatedToString(this Span<byte> span)
    {
        if (span.Length == 0) return string.Empty;

        fixed (byte* b = &MemoryMarshal.GetReference(span))
        {
            var stringSpan = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(b);
            return Encoding.UTF8.GetString(stringSpan);
        }
    }
}
