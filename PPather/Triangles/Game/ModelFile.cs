/*
  This file is part of ppather.

    PPather is free software: you can redistribute it and/or modify
    it under the terms of the GNU Lesser General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    PPather is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Lesser General Public License for more details.

    You should have received a copy of the GNU Lesser General Public License
    along with ppather.  If not, see <http://www.gnu.org/licenses/>.

    Copyright Pontus Borg 2008

 */

using StormDll;

using System;
using System.Buffers;
using System.IO;
using System.Runtime.InteropServices;

namespace Wmo;

public static class ModelFile
{
    public static Model Read(ArchiveSet archive, string fileName)
    {
        using MpqFileStream mpq = archive.GetStream(fileName);

        var pooler = ArrayPool<byte>.Shared;
        byte[] buffer = pooler.Rent((int)mpq.Length);
        mpq.ReadAllBytesTo(buffer);

        using MemoryStream stream = new(buffer, 0, (int)mpq.Length, false);
        using BinaryReader file = new(stream);

        // UPDATED FOR WOTLK 17.10.2008 by toblakai
        // SOURCE: http://www.madx.dk/wowdev/wiki/index.php?title=M2/WotLK

        //_ = file.ReadChars(4);
        //_ = file.ReadUInt32(); // (including \0);
        //                       // check that we have the new known WOTLK Magic 0x80100000
        //                       //PPather.Debug("M2 HEADER VERSION: 0x{0:x8}",
        //                       //    (uint) (version >> 24) | ((version << 8) & 0x00FF0000) | ((version >> 8) & 0x0000FF00) | (version << 24));
        //_ = file.ReadUInt32(); // (including \0);
        //_ = file.ReadUInt32();
        //_ = file.ReadUInt32(); // ? always 0, 1 or 3 (mostly 0);
        //_ = file.ReadUInt32(); //  - number of global sequences;
        //_ = file.ReadUInt32(); //  - offset to global sequences;
        //_ = file.ReadUInt32(); //  - number of animation sequences;
        //_ = file.ReadUInt32(); //  - offset to animation sequences;
        //_ = file.ReadUInt32();
        //_ = file.ReadUInt32(); // Mapping of global IDs to the entries in the Animation sequences block.
        //                       // NOT IN WOTLK uint nD=file.ReadUInt32(); //  - always 201 or 203 depending on WoW client version;
        //                       // NOT IN WOTLK uint ofsD=file.ReadUInt32();
        //_ = file.ReadUInt32(); //  - number of bones;
        //_ = file.ReadUInt32(); //  - offset to bones;
        //_ = file.ReadUInt32(); //  - bone lookup table;
        //_ = file.ReadUInt32();
        file.BaseStream.Seek((sizeof(byte) * 4) + (sizeof(UInt32) * 14), SeekOrigin.Current);

        uint nVertices = file.ReadUInt32(); //  - number of vertices;
        uint ofsVertices = file.ReadUInt32(); //  - offset to vertices;

        //_ = file.ReadUInt32(); //  - number of views (LOD versions?) 4 for every model;
        //                       // NOT IN WOTLK (now in .skins) uint ofsViews=file.ReadUInt32(); //  - offset to views;
        //_ = file.ReadUInt32(); //  - number of color definitions;
        //_ = file.ReadUInt32(); //  - offset to color definitions;
        //_ = file.ReadUInt32(); //  - number of textures;
        //_ = file.ReadUInt32(); //  - offset to texture definitions;
        //_ = file.ReadUInt32(); //  - number of transparency definitions;
        //_ = file.ReadUInt32(); //  - offset to transparency definitions;
        //                       // NOT IN WOTLK uint nTexAnims = file.ReadUInt32(); //  - number of texture animations;
        //                       // NOT IN WOTLK uint ofsTexAnims = file.ReadUInt32(); //  - offset to texture animations;
        //_ = file.ReadUInt32(); //  - always 0;
        //_ = file.ReadUInt32();
        //_ = file.ReadUInt32();
        //_ = file.ReadUInt32();
        //_ = file.ReadUInt32(); //  - number of blending mode definitions;
        //_ = file.ReadUInt32(); //  - offset to blending mode definitions;
        //_ = file.ReadUInt32(); //  - bone lookup table;
        //_ = file.ReadUInt32();
        //_ = file.ReadUInt32(); //  - number of texture lookup table entries;
        //_ = file.ReadUInt32(); //  - offset to texture lookup table;
        //_ = file.ReadUInt32(); //  - texture unit definitions?;
        //_ = file.ReadUInt32();
        //_ = file.ReadUInt32(); //  - number of transparency lookup table entries;
        //_ = file.ReadUInt32(); //  - offset to transparency lookup table;
        //_ = file.ReadUInt32(); //  - number of texture animation lookup table entries;
        //_ = file.ReadUInt32(); //  - offset to texture animation lookup table;

        //float[] theFloats = new float[14]; // Noone knows. Meeh, they are here.
        //for (int i = 0; i < 14; i++)
        //    file.ReadSingle();
        file.BaseStream.Seek((sizeof(UInt32) * 23) + (sizeof(Single) * 14), SeekOrigin.Current);

        uint nBoundingTriangles = file.ReadUInt32();
        uint ofsBoundingTriangles = file.ReadUInt32();
        uint nBoundingVertices = file.ReadUInt32();
        uint ofsBoundingVertices = file.ReadUInt32();

        //_ = file.ReadUInt32();
        //_ = file.ReadUInt32();
        //_ = file.ReadUInt32();
        //_ = file.ReadUInt32();
        //_ = file.ReadUInt32();
        //_ = file.ReadUInt32();
        //_ = file.ReadUInt32();
        //_ = file.ReadUInt32();
        //_ = file.ReadUInt32(); //  - number of lights;
        //_ = file.ReadUInt32(); //  - offset to lights;
        //_ = file.ReadUInt32(); //  - number of cameras;
        //_ = file.ReadUInt32(); //  - offset to cameras;
        //_ = file.ReadUInt32();
        //_ = file.ReadUInt32();
        //_ = file.ReadUInt32(); //  - number of ribbon emitters;
        //_ = file.ReadUInt32(); //  - offset to ribbon emitters;
        //_ = file.ReadUInt32(); //  - number of particle emitters;
        //_ = file.ReadUInt32(); //  - offset to particle emitters;
        file.BaseStream.Seek(sizeof(UInt32) * 18, SeekOrigin.Current);

        pooler.Return(buffer);

        return new(
            fileName,
            ReadVertices(file, nVertices, ofsVertices),
            ReadBoundingTriangles(file, nBoundingTriangles, ofsBoundingTriangles),
            ReadBoundingVertices(file, nBoundingVertices, ofsBoundingVertices));
    }

    private static float[] ReadBoundingVertices(BinaryReader file, uint nVertices, uint ofsVertices)
    {
        if (nVertices == 0)
            return [];

        file.BaseStream.Seek(ofsVertices, SeekOrigin.Begin);
        float[] vertices = new float[nVertices * 3];

        file.Read(MemoryMarshal.Cast<float, byte>(vertices.AsSpan()));

        return vertices;
    }

    private static ushort[] ReadBoundingTriangles(BinaryReader file, uint nTriangles, uint ofsTriangles)
    {
        if (nTriangles == 0)
            return [];

        file.BaseStream.Seek(ofsTriangles, SeekOrigin.Begin);

        ushort[] triangles = new ushort[nTriangles];
        file.Read(MemoryMarshal.Cast<ushort, byte>(triangles.AsSpan()));

        return triangles;
    }

    private static float[] ReadVertices(BinaryReader file, uint nVertices, uint ofcVertices)
    {
        float[] vertices = new float[nVertices * 3];

        file.BaseStream.Seek(ofcVertices, SeekOrigin.Begin);
        for (int i = 0; i < nVertices; i++)
        {
            Span<float> span = vertices.AsSpan(i * 3, 3);
            file.Read(MemoryMarshal.Cast<float, byte>(span));

            //_ = file.ReadUInt32();  // bone weights
            //_ = file.ReadUInt32();  // bone indices

            //_ = file.ReadSingle(); // normal *3
            //_ = file.ReadSingle();
            //_ = file.ReadSingle();

            //_ = file.ReadSingle(); // texture coordinates
            //_ = file.ReadSingle();

            //_ = file.ReadSingle(); // some crap
            //_ = file.ReadSingle();
            file.BaseStream.Seek((sizeof(UInt32) * 2) + (sizeof(Single) * 7), SeekOrigin.Current);
        }
        return vertices;
    }
}
