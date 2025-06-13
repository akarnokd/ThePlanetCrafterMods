// Copyright (c) 2022-2025, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using System;
using System.Collections.Generic;
using System.Text;

namespace LibCommon
{
    /// <summary>
    /// Tracks how many items per type are there.
    /// Uses linear-probing and direct field references to improve
    /// performance over a traditional Dictionary string -> int counter.
    /// Does not support resizing!
    /// </summary>
    internal class DictionaryCounter
    {
        readonly Entry[] entries;
        readonly int MASK;

        internal DictionaryCounter(int N)
        {
            entries = new Entry[N];
            MASK = N - 1;
        }

        internal void Clear()
        {
            Array.Clear(entries, 0, entries.Length);
        }

        internal void Update(string s)
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
                    return;
                }
                if (e.hash == key && e.key == s)
                {
                    e.count++;
                    return;
                }
            }
            throw new InvalidOperationException(nameof(DictionaryCounter) + " overflow!");
        }

        internal bool DeduceIfPositive(string s)
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
                    return false;
                }
                if (e.hash == key && e.key == s)
                {
                    var c = e.count;
                    if (c != 0)
                    {
                        e.count = c - 1;
                        return true;
                    }
                    return false;
                }
            }
            throw new InvalidOperationException(nameof(DictionaryCounter) + " overflow!");
        }

        internal int CountOf(string s)
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
                    return 0;
                }
                if (e.hash == key && e.key == s)
                {
                    return e.count;
                }
            }
            return 0;
        }

        internal struct Entry
        {
            internal string key;
            internal int hash;
            internal int count;
        }
    }
}
