// Copyright (c) 2022-2025, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using HarmonyLib;
using System;
using System.Reflection;

namespace LibCommon
{
    /// <summary>
    /// Methods to verify the patches use the correct parameter types when overriding,
    /// as Harmony apparently doesn't check them, and can lead to Unity-level memory corruptions.
    /// </summary>
    public static class HarmonyIntegrityCheck
    {
        /// <summary>
        /// Performs the checks on the HarmonyPatch annotated methods of the patch class
        /// against their intended targets
        /// </summary>
        /// <param name="type">The class having the HarmonyPatch annotations</param>
        /// <exception cref="InvalidCastException">When a mismatch is found first.</exception>
        public static void Check(Type type)
        {
            foreach (var patchMethod in type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                var harmonyPatchAttr = patchMethod.GetCustomAttribute<HarmonyPatch>();
                if (harmonyPatchAttr != null)
                {
                    var targetType = harmonyPatchAttr.info.declaringType;
                    var methodName = harmonyPatchAttr.info.methodName;

                    foreach (var targetMethod in AccessTools.GetDeclaredMethods(targetType))
                    {
                        if (targetMethod.Name == methodName)
                        {
                            foreach (var patchParameter in patchMethod.GetParameters())
                            {
                                foreach (var targetParameter in targetMethod.GetParameters())
                                {
                                    if (!patchParameter.Name.StartsWith("__") && patchParameter.Name == targetParameter.Name)
                                    {
                                        if ((patchParameter.ParameterType.IsByRef && !targetParameter.ParameterType.IsByRef && patchParameter.ParameterType.GetElementType() != targetParameter.ParameterType) 
                                            || (patchParameter.ParameterType.IsByRef == targetParameter.ParameterType.IsByRef && patchParameter.ParameterType != targetParameter.ParameterType))
                                        {
                                            throw new InvalidCastException(
                                                "Patch declares different parameter type(s) than the overridden method!\r\n"
                                                + " at Patch method   : " + patchMethod + "\r\n"
                                                + " at Original method: " + targetMethod + "\r\n"
                                                + " at Patch param    : " + patchParameter + "\r\n"
                                                + " at Original param : " + targetParameter + "\r\n"
                                            ) ;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
