// Copyright (c) 2022-2025, David Karnok & Contributors
// Licensed under the Apache License, Version 2.0

using BepInEx.Bootstrap;
using SpaceCraft;
using System.Text;
using Unity.Netcode;

namespace LibCommon
{
    /// <summary>
    /// Stores all the installed mod names and versions in a hidden Iron object's text.
    /// </summary>
    internal class SaveModInfo
    {
        const int storeInWorldObjectId = 900_000_000;
        const int storeInWorldObjectIdOld = 90_000_000;

        internal static void Save()
        {
            if (NetworkManager.Singleton.IsServer)
            {
                var sb = new StringBuilder();

                foreach (var pi in Chainloader.PluginInfos)
                {
                    if (sb.Length > 0)
                    {
                        sb.Append(";");
                    }
                    sb.Append(pi.Key).Append("=").Append(pi.Value.Metadata.Version);
                }

                var wo = WorldObjectsHandler.Instance.GetWorldObjectViaId(storeInWorldObjectId);
                if (wo == null)
                {
                    var gr = GroupsHandler.GetGroupViaId("Iron");
                    if (gr == null)
                    {
                        return;
                    }
                    wo = WorldObjectsHandler.Instance.CreateNewWorldObject(gr, storeInWorldObjectId);
                    wo.SetDontSaveMe(false);
                }
                wo.SetText(sb.ToString().Replace("@", " ").Replace("|", " "));

                // remove the old entry
                WorldObjectsHandler.Instance.GetAllWorldObjects().Remove(storeInWorldObjectIdOld);
            }
        }
    }
}
