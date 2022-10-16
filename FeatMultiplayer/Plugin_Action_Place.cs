using BepInEx;
using HarmonyLib;
using SpaceCraft;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace FeatMultiplayer
{
    public partial class Plugin : BaseUnityPlugin
    {
        static bool cancelBuildAfterPlace;

        /// <summary>
        /// The vanilla game uses ConstructibleGhost::Place to place the object, play some animation and be done with it.
        /// 
        /// On the client, we can't let this happen as it would make the object disappear/flicker due to full syncs.
        /// Instead, we have to dig into what was being constructed: a panel or a full building.
        /// For the building, we just ask the host to build it for us. For a panel, we have to figure out what the
        /// parent building is, what the panel index is and what's the new panel's id built.
        /// </summary>
        /// <param name="__instance">The greenish semi-transparent ghost object.</param>
        /// <param name="__result">On the client, we pretend as if the build didn't happen. Let the host make all world and inventory changes.</param>
        /// <param name="___groupConstructible">What object was built.</param>
        /// <returns>True on the host, false on the client if the positioning was valid and we suppressed the placement.</returns>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(ConstructibleGhost), nameof(ConstructibleGhost.Place))]
        static bool ConstructibleGhost_Place(ConstructibleGhost __instance,
            ref GameObject __result, GroupConstructible ___groupConstructible)
        {
            cancelBuildAfterPlace = false;
            if (updateMode == MultiplayerMode.CoopClient)
            {
                bool positioningStatus = __instance.gameObject.GetComponent<GhostPlacementChecker>().GetPositioningStatus();
                if (positioningStatus)
                {
                    ConstraintSamePanel component = __instance.gameObject.GetComponent<ConstraintSamePanel>();
                    if (component != null)
                    {
                        var aimPanel = component.GetAimedAtPanel();
                        var newPanelType = component.GetAssociatedSubPanelType();
                        var assoc = aimPanel.GetWorldObjectAssociated();

                        int idx = 0;
                        var panels = assoc.gameObject.GetComponentsInChildren<Panel>();
                        foreach (Panel panel in panels)
                        {
                            if (panel == aimPanel)
                            {

                                MessagePanelChanged pc = new MessagePanelChanged()
                                {
                                    itemId = assoc.GetWorldObject().GetId(),
                                    panelId = idx,
                                    panelType = (int)newPanelType,
                                    panelGroupId = ___groupConstructible.GetId()
                                };
                                PlayBuildGhost();
                                LogInfo("Place: Change Panel " + pc.itemId + ", " + idx + ", " + newPanelType);
                                SendHost(pc, true);
                                break;
                            }
                            idx++;
                        }
                        if (idx == panels.Length)
                        {
                            LogWarning("Place: Panel not found");
                        }
                    }
                    else
                    {
                        var mpc = new MessagePlaceConstructible()
                        {
                            groupId = ___groupConstructible.GetId(),
                            position = __instance.gameObject.transform.position,
                            rotation = __instance.gameObject.transform.rotation
                        };
                        PlayBuildGhost();
                        LogInfo("Place: Construct " + mpc.groupId + "; " + mpc.position + "; " + mpc.rotation);
                        SendHost(mpc, true);
                    }
                    Destroy(__instance.gameObject);
                    __result = null;
                    cancelBuildAfterPlace = true;
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// The vanilla game uses ConstructibleGhost::Place to place the object, play some animation and be done with it.
        /// 
        /// On the host, we let the build happen and send the new built object to the client.
        /// </summary>
        /// <param name="__instance">The greenish semi-transparent ghost object.</param>
        /// <param name="__result">On the client, we pretend as if the build didn't happen. Let the host make all world and inventory changes.</param>
        /// <param name="___groupConstructible">What object was built.</param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(ConstructibleGhost), nameof(ConstructibleGhost.Place))]
        static void ConstructibleGhost_Place_Post(ConstructibleGhost __instance,
            GameObject __result, GroupConstructible ___groupConstructible)
        {
            if (updateMode == MultiplayerMode.CoopHost)
            {
                if (__result != null)
                {
                    var woa = __result.GetComponent<WorldObjectAssociated>();
                    if (woa == null)
                    {
                        woa = __result.GetComponentInParent<WorldObjectAssociated>();
                    }
                    if (woa != null)
                    {
                        var wo = woa.GetWorldObject();
                        if (wo != null)
                        {
                            // Sync back any color or text
                            if (woa.TryGetComponent<WorldObjectColor>(out var wcolor))
                            {
                                wo.SetColor(wcolor.GetColor());
                            }
                            if (woa.TryGetComponent<WorldObjectText>(out var wtext))
                            {
                                wo.SetText(wtext.GetText());
                            }
                            SendWorldObjectToClients(wo, false);
                            return;
                        }
                    }
                    LogWarning("Place: WorldObjectAssociated not found");
                }
            }
        }

        static void PlayBuildGhost()
        {
            PlayerMainController pm = GetPlayerMainController();
            if (pm != null)
            {
                pm.GetPlayerAudio().PlayBuildGhost();

                var anims = pm.GetAnimations();
                anims.AnimateConstruct(true);
                anims.StartCoroutine(StopConstructAnim(anims));
            }
        }

        static IEnumerator StopConstructAnim(PlayerAnimations pa)
        {
            yield return new WaitForSeconds(0.5f);
            pa.AnimateConstruct(false);
        }

        /// <summary>
        /// The vanilla game calls PlayerBuilder::InputOnAction for the mouse click (which then calls ConstructibleGhost::Place).
        /// 
        /// We try to keep up the multi-build mode if the <see cref="ConstructibleGhost_Place(ConstructibleGhost, ref GameObject, GroupConstructible)"/>
        /// wanted to close the window for a valid build.
        /// </summary>
        /// <param name="__instance">The instance so we can cancel the build.</param>
        /// <param name="_isPressingAccessibilityKey">If the user wanted multi-build</param>
        /// <param name="___ghost">Clear the ghost as the vanilla game does not do this and may result in being unable to build again.</param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayerBuilder), nameof(PlayerBuilder.InputOnAction))]
        static void PlayerBuilder_InputOnAction(
            PlayerBuilder __instance,
            bool _isPressingAccessibilityKey, 
            ref ConstructibleGhost ___ghost,
            GroupConstructible ___ghostGroupConstructible
        )
        {
            if (cancelBuildAfterPlace)
            {
                cancelBuildAfterPlace = false;

                __instance.GetComponent<PlayerAudio>().PlayBuildGhost();
                __instance.GetComponent<PlayerAnimations>().AnimateConstruct(true);
                __instance.Invoke("StopAnimation", 0.5f);

                Inventory inv = GetPlayerMainController().GetPlayerBackpack().GetInventory();

                var itemItself = new List<Group>() { ___ghostGroupConstructible };
                var recipe = ___ghostGroupConstructible.GetRecipe().GetIngredientsGroupInRecipe();
                suppressInventoryChange = true;
                try
                {
                    if (inv.ContainsItems(itemItself))
                    {
                        inv.RemoveItems(itemItself, false, true);
                    }
                    else
                    {
                        inv.RemoveItems(recipe, false, true);
                    }
                }
                finally
                {
                    suppressInventoryChange = false;
                }
                if (_isPressingAccessibilityKey && recipe.Count > 0)
                {
                    ___ghost = null;
                    __instance.SetNewGhost(___ghostGroupConstructible);
                }
                else
                {
                    __instance.InputOnCancelAction();
                    ___ghost = null;
                }
            }
        }

        /// <summary>
        /// The vanilla game calls WorldObjectAssociated::RefreshPanelsId is called
        /// to sync back the current panels of the GameObject to the panel ids
        /// in the WorldObject.
        /// 
        /// This happens when a panel is constructed, deconstructed, or the
        /// entire building gets deconstructed so adjacent buildings have to reset their
        /// exterior panels.
        /// 
        /// On the host, we have to intercept this for now as a full sync may just
        /// post an open building with the wrong panels until the next sync happens.
        /// 
        /// We do nothing on the client.
        /// </summary>
        /// <param name="___worldObjectAssociated">The associated building WorldObject.</param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(WorldObjectAssociated), nameof(WorldObjectAssociated.RefreshPanelsId))]
        static void WorldObjectAssociated_RefreshPanelsId(WorldObject ___worldObjectAssociated)
        {
            if (updateMode == MultiplayerMode.CoopHost)
            {
                SendWorldObjectToClients(___worldObjectAssociated, false);
            }
        }

        static void ReceiveMessagePlaceConstructible(MessagePlaceConstructible mpc)
        {
            GroupConstructible gc = GroupsHandler.GetGroupViaId(mpc.groupId) as GroupConstructible;
            if (gc != null)
            {
                LogInfo("ReceiveMessagePlaceConstructible: " + mpc.groupId + ", " + mpc.position + ", " + mpc.rotation);
                var wo = WorldObjectsHandler.CreateNewWorldObject(gc, WorldObjectsIdHandler.GetNewWorldObjectIdForDb());
                wo.SetPositionAndRotation(mpc.position, mpc.rotation);
                var go = WorldObjectsHandler.InstantiateWorldObject(wo, _fromDb: false);

                // Sync back any color or text
                if (go.TryGetComponent<WorldObjectColor>(out var wcolor))
                {
                    wo.SetColor(wcolor.GetColor());
                }
                if (go.TryGetComponent<WorldObjectText>(out var wtext))
                {
                    wo.SetText(wtext.GetText());
                }

                SendWorldObjectToClients(wo, false);

                ClientConsumeRecipe(gc, mpc.sender.shadowBackpack);
            }
            else
            {
                LogInfo("ReceiveMessagePlaceConstructible: Unknown constructible " + mpc.groupId + ", " + mpc.position + ", " + mpc.rotation);
            }
        }

        static void ReceiveMessagePanelChanged(MessagePanelChanged mpc)
        {
            WorldObject wo = WorldObjectsHandler.GetWorldObjectViaId(mpc.itemId);
            if (wo != null)
            {
                if (TryGetGameObject(wo, out GameObject go))
                {
                    LogInfo("ReceiveMessagePanelChanged: " + mpc.itemId + ", " + mpc.panelId + ", " + mpc.panelType);
                    var panelIds = wo.GetPanelsId();
                    if (panelIds == null)
                    {
                        panelIds = new List<int>();
                    }
                    while (panelIds.Count <= mpc.panelId)
                    {
                        panelIds.Add(1);
                    }
                    panelIds[mpc.panelId] = mpc.panelType;
                    wo.SetPanelsId(panelIds);
                    UpdatePanelsOn(wo);

                    GroupConstructible gc = (GroupConstructible)GroupsHandler.GetGroupViaId(mpc.panelGroupId);
                    ClientConsumeRecipe(gc, mpc.sender.shadowBackpack);

                    SendWorldObjectToClients(wo, false);
                }
                else
                {
                    LogWarning("ReceiveMessagePanelChanged: GameObject not found for: " + mpc.itemId + ", " + mpc.panelId + ", " + mpc.panelType);
                }
            }
            else
            {
                LogWarning("ReceiveMessagePanelChanged: Unknown item: " + mpc.itemId + ", " + mpc.panelId + ", " + mpc.panelType);
            }
        }
    }
}
