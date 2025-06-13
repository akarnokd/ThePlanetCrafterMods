// Copyright (c) 2022-2025, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using System;
using System.Collections.Generic;
using System.Text;

namespace LibCommon
{
    /// <summary>
    /// Tracks how many stacks of items per type are in an inventory listing.
    /// Uses linear-probing and direct field references to improve
    /// performance over a traditional Dictionary string -> int counter.
    /// Does not support resizing!
    /// </summary>
    internal class DictionaryStackCounter
    {
        readonly Entry[] entries;
        readonly int MASK;

        internal DictionaryStackCounter(int N)
        {
            entries = new Entry[N];
            MASK = N - 1;
        }

        internal void Clear()
        {
            Array.Clear(entries, 0, entries.Length);
        }

        internal void Update(string s, int sizePerGroup, ref int groups)
        {
            var entries = this.entries;
            var n = entries.Length;
            int key = s.GetHashCode() & 0x7FFF_FFFF;
            int m = MASK;
            for (int k = 0; k < n; k++)
            {
                ref var e = ref entries[(k + key) & m];
                if (e.key == null)
                {
                    e.key = s;
                    e.hash = key;
                    e.count = 1;
                    groups++;
                    return;
                }
                if (e.hash == key && e.key == s)
                {
                    var c = e.count;
                    c++;
                    if (c > sizePerGroup)
                    {
                        c = 1;
                        groups++;
                    }
                    e.count = c;
                    return;
                }
            }
            throw new InvalidOperationException(nameof(DictionaryStackCounter) + " overflow!");
        }

        internal struct Entry
        {
            internal string key;
            internal int hash;
            internal int count;
        }
    }
}
