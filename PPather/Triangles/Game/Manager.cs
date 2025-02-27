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

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Wmo;

public abstract class Manager<T>
{
    private readonly Dictionary<string, T> items;

    public Manager()
    {
        items = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
    }

    public abstract bool Load(string path, out T t);

    public void Clear()
    {
        items.Clear();
    }

    public T AddAndLoadIfNeeded(string path)
    {
        ref T t = ref CollectionsMarshal.GetValueRefOrAddDefault(items, path, out bool exists);
        if (!exists && Load(path, out t))
        {

        }

        return t;
    }
}
