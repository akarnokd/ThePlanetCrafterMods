// Copyright (c) 2022-2024, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using SpaceCraft;
using UnityEngine;

namespace LibCommon
{
    /// <summary>
    /// Utility methods to check if a actionable object is inside a display case or is being shown
    /// as part of a crafter machine.
    /// This check avoids grabbing such items and having the game delete the display case or the machine. 
    /// </summary>
    public static class GrabChecker
    {
        /// <summary>
        /// Check if the given Actionable instance is shown in a display case or
        /// is being shown temporary in an Auto-Crafter, Crafting station or biolab
        /// as part of the crafting animation.
        /// </summary>
        /// <param name="ag">The actionnable to check</param>
        /// <returns>True if being displayed, false if lose.</returns>
        public static bool IsOnDisplay(Actionnable ag)
        {
            return ag.GetComponentInParent<InventoryShowContent>() != null
                || ag.GetComponentInParent<ActionCrafter>() != null
                || ag.GetComponentInParent<MachineAutoCrafter>() != null
                || IsInsideBiolab(ag);
        }

        static bool IsInsideBiolab(Actionnable ag)
        {
            GameObject o = ag?.gameObject;
            while (o != null)
            {
                if (o.name.Contains("VegetubeCrafter"))
                {
                    return true;
                }
                if (o.transform.parent != null)
                {
                    o = o.transform.parent.gameObject;
                }
                else
                {
                    break;
                }
            }
            return false;
        }
    }
}
