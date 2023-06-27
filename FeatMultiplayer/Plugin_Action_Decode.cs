using BepInEx;
using HarmonyLib;
using SpaceCraft;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FeatMultiplayer.MessageTypes;

namespace FeatMultiplayer
{
    public partial class Plugin : BaseUnityPlugin
    {
        /// <summary>
        /// The vanilla game calls this when a Blueprint Chip is decoded by the button click.
        /// On the client, we need to send a MicrochipUnlock message to the host so it can decode, and sync up the results.
        /// On the host, we need to decode as usual, then notify the client about the new unlock.
        /// </summary>
        /// <param name="__instance">The UI window instance</param>
        /// <param name="___groupChip">The Group object of the blueprint chip itself, to be removed from the inventory upon successful decoding.</param>
        /// <returns>true in singleplayer, false in multiplayer</returns>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(UiWindowBlueprints), nameof(UiWindowBlueprints.DecodeBlueprint))]
        static bool UiWindowBlueprints_DecodeBlueprint(UiWindowBlueprints __instance, 
            Group ___groupChip, Group ___lastSpecificChipFound)
        {
            if (updateMode != MultiplayerMode.SinglePlayer)
            {
                __instance.containerNeverUnlocked.SetActive(false);
                __instance.containerList.SetActive(false);
                if (updateMode == MultiplayerMode.CoopClient)
                {
                    SendHost(new MessageMicrochipUnlock(), true);
                }
                else
                {
                    if (___lastSpecificChipFound != null)
                    {
                        Managers.GetManager<UnlockingHandler>().PlayAudioOnDecode();
                        GetPlayerMainController().GetPlayerBackpack().GetInventory()
                            .RemoveItems(new List<Group> { ___lastSpecificChipFound }, true, true);
                        var unlocked = ((GroupItem)___lastSpecificChipFound).GetUnlocksGroup();
                        GroupsHandler.UnlockGroupGlobally(unlocked);

                        SendAllClients(new MessageMicrochipUnlock()
                        {
                            groupId = unlocked.GetId()
                        }, true);
                    }
                    else
                    {
                        var unlocked = Managers.GetManager<UnlockingHandler>().GetUnlockableGroup();
                        if (unlocked != null)
                        {
                            Managers.GetManager<UnlockingHandler>().PlayAudioOnDecode();
                            GetPlayerMainController().GetPlayerBackpack().GetInventory()
                                .RemoveItems(new List<Group> { ___groupChip }, true, true);
                            GroupsHandler.UnlockGroupGlobally(unlocked);

                            SendAllClients(new MessageMicrochipUnlock()
                            {
                                groupId = unlocked.GetId()
                            }, true);
                        }
                        else
                        {
                            Managers.GetManager<BaseHudHandler>().DisplayCursorText("UI_warn_no_more_chip_to_unlock", 3f, "");
                        }
                    }
                }
                __instance.CloseAll();
                return false;
            }
            return true;
        }

        static void ReceiveMessageUnlocks(MessageUnlocks mu)
        {
            if (updateMode == MultiplayerMode.CoopClient)
            {
                List<Group> grs = new List<Group>();
                foreach (string gid in mu.groupIds)
                {
                    Group gr = GroupsHandler.GetGroupViaId(gid);
                    if (gr != null)
                    {
                        grs.Add(gr);
                    }
                    else
                    {
                        LogWarning("ReceiveMessageUnlocks: Unknown groupId " + gid);
                    }
                }
                GroupsHandler.SetUnlockedGroups(grs);
            }
        }

        static void ReceiveMessageMicrochipUnlock(MessageMicrochipUnlock mmu)
        {
            // Signal back the client immediately
            if (updateMode == MultiplayerMode.CoopHost)
            {
                GroupItem lastSpecificChipFound = null;

                foreach (var wo in mmu.sender.shadowBackpack.GetInsideWorldObjects())
                {
                    if (wo.GetGroup() is GroupItem gri && gri.GetUnlocksGroup() != null)
                    {
                        lastSpecificChipFound = gri;
                    }
                }

                if (lastSpecificChipFound != null)
                {
                    var inv = mmu.sender.shadowBackpack;
                    inv.RemoveItems(new List<Group>() { lastSpecificChipFound }, true, false);
                    var unlocked = lastSpecificChipFound.GetUnlocksGroup();
                    mmu.groupId = unlocked.GetId();
                    GroupsHandler.UnlockGroupGlobally(unlocked);
                }
                else
                {
                    var gr = Managers.GetManager<UnlockingHandler>().GetUnlockableGroup();
                    if (gr != null)
                    {
                        mmu.groupId = gr.GetId();
                        var inv = mmu.sender.shadowBackpack;
                        var microGroup = GroupsHandler.GetGroupViaId("BlueprintT1");
                        inv.RemoveItems(new List<Group>() { microGroup }, true, false);
                        GroupsHandler.UnlockGroupGlobally(gr);
                    }
                    else
                    {
                        mmu.groupId = "";
                    }
                }
                SendAllClients(mmu, true);
            }
            else
            {
                if (mmu.groupId != "")
                {
                    var unlocked = GroupsHandler.GetGroupViaId(mmu.groupId);
                    if (unlocked != null)
                    {
                        LogInfo("ReceiveMessageMicrochipUnlock: Unlock " + mmu.groupId);
                        GroupsHandler.UnlockGroupGlobally(unlocked);
                        Managers.GetManager<UnlockingHandler>().PlayAudioOnDecode();
                    }
                    else
                    {
                        LogError("ReceiveMessageMicrochipUnlock: Unknown group " + mmu.groupId);
                    }
                }
                else
                {
                    Managers.GetManager<BaseHudHandler>().DisplayCursorText("UI_warn_no_more_chip_to_unlock", 3f, "");
                }
            }
        }
    }
}
