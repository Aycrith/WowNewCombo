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

namespace Wmo;

public sealed class ModelManager : Manager<Model>
{
    private readonly ArchiveSet archive;

    private readonly SearchValues<string> lookup = SearchValues.Create([".mdx", ".mdl"], StringComparison.OrdinalIgnoreCase);

    public ModelManager(ArchiveSet archive)
    {
        this.archive = archive;
    }

    public override bool Load(string path, out Model t)
    {
        string extension = Path.GetExtension(path);

        if (lookup.Contains(extension))
        {
            path = Path.ChangeExtension(path, ".m2");
        }

        t = ModelFile.Read(archive, path);
        return true;
    }
}
