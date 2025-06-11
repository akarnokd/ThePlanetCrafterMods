// Copyright (c) 2022-2025, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using System;
using UnityEngine.InputSystem;

namespace CheatInventoryStacking
{
    internal class HashGroupCounter
    {
        readonly Entry[] entries;
        readonly int MASK;

        internal HashGroupCounter(int N)
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
            int key = s.GetHashCode();
            int m = MASK;
            for (int k = 0; k < entries.Length; k++)
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
            throw new InvalidOperationException("HashGroupCounter full?!");
        }

        internal struct Entry
        {
            internal string key;
            internal int hash;
            internal int count;
        }
    }
}
