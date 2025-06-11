// Copyright (c) 2022-2025, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using SpaceCraft;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace LibCommon
{
    /// <summary>
    /// Handles the genetics information of GeneticTrait and DNASequence world object types.
    /// </summary>
    public static class GeneticsGrouping
    {
        /// <summary>
        /// Generates a stack id that considers the genetic trait, DNA sequence information
        /// or blueprints
        /// as these should not stack based on their group id alone.
        /// </summary>
        /// <param name="wo"></param>
        /// <returns></returns>
        public static string GetStackId(WorldObject wo)
        {
            var grid = wo.GetGroup().id;
            if (ReferenceEquals(grid, "GeneticTrait"))
            {
                var sb = new StringBuilder(48);
                sb.Append(grid).Append("_");
                AppendTraitInfo(wo.GetGeneticTraitType(), wo.GetGeneticTraitValue(), wo.GetColor(), sb);
                return sb.ToString();
            }
            else if (ReferenceEquals(grid, "DNASequence"))
            {
                var sb = new StringBuilder(128);
                var inv = InventoriesHandler.Instance.GetInventoryById(wo.GetLinkedInventoryId());
                if (inv != null)
                {
                    sb.Append(grid);
                    List<WorldObject> traits = [.. inv.GetInsideWorldObjects()];
                    traits.Sort(traitSorter);
                    foreach (var wo2 in traits)
                    {
                        sb.Append('_');
                        AppendTraitInfo(wo2.GetGeneticTraitType(), wo2.GetGeneticTraitValue(), wo2.GetColor(), sb);
                    }
                }
                return sb.ToString();
            }
            else if (ReferenceEquals(grid, "BlueprintT1"))
            {
                var sb = new StringBuilder(128);
                sb.Append(grid);
                var lg = wo.GetLinkedGroups();
                if (lg != null && lg.Count > 0)
                {
                    sb.Append('_');
                    sb.Append(lg[0].id);
                }
                return sb.ToString();
            }
            return grid;
        }

        /// <summary>
        /// Given a genetic type, value and color, append their string representation to the builder.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="value"></param>
        /// <param name="color"></param>
        /// <param name="sb"></param>
        public static void AppendTraitInfo(DataConfig.GeneticTraitType type, int value, Color color, StringBuilder sb)
        {
            sb.Append((int)type);
            sb.Append('_');
            if (type == DataConfig.GeneticTraitType.ColorA 
                || type == DataConfig.GeneticTraitType.ColorB
                || type == DataConfig.GeneticTraitType.PatternColor)
            {
                sb.Append(((int)(color.r * 255)).ToString("X2"));
                sb.Append(((int)(color.g * 255)).ToString("X2"));
                sb.Append(((int)(color.b * 255)).ToString("X2"));
            }
            else
            {
                sb.Append(value);
            }
        }

        /// <summary>
        /// Given a genetic type and value, append their string representation to the builder.
        /// If the type is a color, the value is converted to an RRGGBB hex int
        /// </summary>
        /// <param name="type"></param>
        /// <param name="value"></param>
        /// <param name="sb"></param>
        public static void AppendTraitInfo(int type, int value, StringBuilder sb)
        {
            sb.Append(type);
            sb.Append('_');
            if (type == (int)DataConfig.GeneticTraitType.ColorA
                || type == (int)DataConfig.GeneticTraitType.ColorB
                || type == (int)DataConfig.GeneticTraitType.PatternColor)
            {
                sb.Append(value.ToString("X6"));
            }
            else
            {
                sb.Append(value);
            }
        }

        static readonly Comparison<WorldObject> traitSorter = (a, b) => a.GetGeneticTraitType().CompareTo(b.GetGeneticTraitType());
    }
}
