// Copyright (c) 2022-2024, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using System.Runtime.CompilerServices;

namespace LibCommon
{
    /// <summary>
    /// Extension methods to work with Unity objects in modern C#.
    /// </summary>
    public static class UnityExt
    {
        /// <summary>
        /// Returns a proper null if the given input Unity object is destroyed
        /// so null-coalescing works properly.
        /// </summary>
        /// <typeparam name="T">The type of the Unity Object.</typeparam>
        /// <param name="obj">The value to return as is or null if the object is destroyed.</param>
        /// <returns>The input object or null.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T AsNullable<T>(this T obj) where T : UnityEngine.Object
        {
            if (obj == null)
            {
                return null;
            }
            return obj;
        }
    }
}
