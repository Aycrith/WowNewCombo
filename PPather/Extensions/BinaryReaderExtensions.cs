using System;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;

namespace PPather.Extensions;

public static class BinaryReaderExtensions
{
    public static Vector3 ReadVector3(this BinaryReader b)
    {
        Span<float> v3 = stackalloc float[3];
        b.Read(MemoryMarshal.Cast<float, byte>(v3));
        return new(v3);
    }
}
