// Copyright (c) 2022-2025, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using System;

namespace LibCommon
{
    /// <summary>
    /// Tracks the first encounter of items by their string id.
    /// Uses linear-probing and direct field accesses to
    /// improve upon the traditional HashSet.Add approach
    /// Does not support resizing!
    /// </summary>
    internal class HashSetFast
    {
        readonly Entry[] entries;
        readonly int MASK;

        internal int Count;
        internal HashSetFast(int capacity)
        {
            entries = new Entry[capacity];
            MASK = capacity - 1;
        }

        internal bool Add(string entry)
        {
            var entries = this.entries;
            var n = entries.Length;
            var m = MASK;

            var h = entry.GetHashCode() & 0x7FFF_FFFF;

            for (int i = 0; i < n; i++)
            {
                ref Entry e = ref entries[(i + h) & m];
                if (e.key == null)
                {
                    e.key = entry;
                    e.hash = h;
                    Count++;
                    return true;
                }
                else if (e.hash == h && e.key == entry)
                {
                    return false;
                }
            }
            throw new InvalidOperationException(nameof(HashSetFast) + " overflow!");
        }

        internal void Clear()
        {
            Array.Fill(entries, default);
            Count = 0;
        }

        internal struct Entry
        {
            internal string key;
            internal int hash;
        }
    }
}
