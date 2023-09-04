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

    public static Vector3 ReadVector3_XZY(this BinaryReader b)
    {
        Span<float> v3 = stackalloc float[3];
        b.Read(MemoryMarshal.Cast<float, byte>(v3));

        // from format
        // X Z -Y
        // to format
        // X Y Z
        (v3[1], v3[2]) = (-v3[2], v3[1]);

        return new(v3);
    }

    public static bool EOF(this BinaryReader b)
    {
        Stream s = b.BaseStream;
        return s.Position >= s.Length;
    }

}
