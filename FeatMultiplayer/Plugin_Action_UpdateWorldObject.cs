using BepInEx;
using SpaceCraft;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using FeatMultiplayer.MessageTypes;

namespace FeatMultiplayer
{
    public partial class Plugin : BaseUnityPlugin
    {
        static float epsilon = 0.001f;

        static bool IsChanged(Vector3 vec1, Vector3 vec2)
        {
            var v3 = vec1 - vec2;
            return Mathf.Abs(v3.x) > epsilon
                || Mathf.Abs(v3.y) > epsilon
                || Mathf.Abs(v3.z) > epsilon;
        }

        static bool IsChanged(Quaternion q1, Quaternion q2)
        {
            return Mathf.Abs(q1.x - q2.x) > epsilon
                || Mathf.Abs(q1.y - q2.y) > epsilon
                || Mathf.Abs(q1.z - q2.z) > epsilon
                || Mathf.Abs(q1.w - q2.w) > epsilon;
        }
        static bool IsChanged(Color q1, Color q2)
        {
            return Mathf.Abs(q1.r - q2.r) > epsilon
                || Mathf.Abs(q1.g - q2.g) > epsilon
                || Mathf.Abs(q1.b - q2.b) > epsilon
                || Mathf.Abs(q1.a - q2.a) > epsilon;
        }

        static string DebugWorldObject(int id)
        {
            var wo = WorldObjectsHandler.GetWorldObjectViaId(id);
            if (wo == null)
            {
                return "null";
            }
            return DebugWorldObject(wo);
        }

        static string DebugWorldObject(WorldObject wo)
        {
            if (wo == null)
            {
                return "{ null }";
            }
            StringBuilder sb = new StringBuilder();
            sb.Append("{ id=").Append(wo.GetId())
            .Append(", groupId = ");
            if (wo.GetGroup() != null)
            {
                sb.Append(wo.GetGroup().GetId());
            }
            else
            {
                sb.Append("null");
            }
            if (wo.GetIsPlaced())
            {
                sb.Append(", position = ").Append(wo.GetPosition());
            }
            if (wo.GetLinkedInventoryId() > 0)
            {
                sb.Append(", inventory = ").Append(wo.GetLinkedInventoryId());
            }
            if (wo.GetGrowth() > 0f)
            {
                sb.Append(", growth = ").Append(wo.GetGrowth().ToString(CultureInfo.InvariantCulture));
            }
            if (wo.GetGameObject() != null)
            {
                sb.Append(", GameObject");
            }
            sb.Append(" }");
            return sb.ToString();
        }

        static void ReceiveMessageAllObjects(MessageAllObjects mc)
        {
            if (updateMode == MultiplayerMode.CoopClient)
            {
                // LogInfo("Received all constructs: " + mc.worldObjects.Count);
                HashSet<int> toDelete = new HashSet<int>();

                foreach (var kv in worldObjectById)
                {
                    if (!kv.Value.GetDontSaveMe())
                    {
                        toDelete.Add(kv.Key);
                    }
                }

                foreach (MessageWorldObject mwo in mc.worldObjects)
                {
                    //LogInfo("WorldObject " + mwo.id + " - " + mwo.groupId + " at " + mwo.position);
                    toDelete.Remove(mwo.id);

                    UpdateWorldObject(mwo);
                }

                foreach (int id in toDelete)
                {
                    if (worldObjectById.TryGetValue(id, out var wo))
                    {
                        if (TryGetGameObject(wo, out var go))
                        {
                            LogInfo("ReceiveMessageAllObjects: Destroying " + DebugWorldObject(id));
                            LogInfo("ReceiveMessageAllObjects:   GameObject destroyed at " + go.transform.position);
                            TryRemoveGameObject(wo);
                            Destroy(go);
                        }
                        WorldObjectsHandler.DestroyWorldObject(wo);
                    }
                    else {
                        LogWarning("ReceiveMessageAllObjects: Unknown WorldObject " + id);
                    }
                }
            }
        }

        static void SendWorldObjectToClients(WorldObject worldObject, bool makeGrabable)
        {
            SendAllClients(CreateUpdateWorldObject(worldObject, makeGrabable), true);
        }

        static void SendWorldObjectToHost(WorldObject worldObject, bool makeGrabable)
        {
            SendHost(CreateUpdateWorldObject(worldObject, makeGrabable), true);
        }

        static void SendWorldObjectTo(WorldObject worldObject, bool makeGrabable, ClientConnection cc)
        {
            cc.Send(CreateUpdateWorldObject(worldObject, makeGrabable));
            cc.Signal();
        }

        static string CreateUpdateWorldObject(WorldObject worldObject, bool makeGrabable)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("UpdateWorldObject|");
            MessageWorldObject.AppendWorldObject(sb, '|', worldObject, makeGrabable);
            sb.Append('\n');
            return sb.ToString();
        }

        static void ReceiveMessageUpdateWorldObject(MessageUpdateWorldObject mc)
        {
            LogInfo("ReceiveMessageUpdateWorldObject: " + mc.worldObject.id + ", " + mc.worldObject.groupId);
            UpdateWorldObject(mc.worldObject);
            if (updateMode == MultiplayerMode.CoopHost)
            {
                SendAllClientsExcept(mc.sender.id, mc, true);
            }
        }

        static void UpdatePanelsOn(WorldObject wo)
        {
            if (TryGetGameObject(wo, out var go))
            {
                var panelIds = wo.GetPanelsId();
                if (panelIds != null && panelIds.Count > 0)
                {
                    Panel[] componentsInChildren = go.GetComponentsInChildren<Panel>();
                    int num = 0;
                    foreach (Panel panel in componentsInChildren)
                    {
                        if (num < panelIds.Count)
                        {
                            try
                            {
                                DataConfig.BuildPanelSubType subPanelType = (DataConfig.BuildPanelSubType)panelIds[num];
                                panel.ChangePanel(subPanelType);
                            }
                            catch (Exception ex)
                            {
                                LogError(ex);
                            }
                        }
                        num++;
                    }
                    LogInfo("UpdateWorldObject:   Updating panels on " + wo.GetId() + " success | " + string.Join(", ", panelIds));
                }
                else
                {
                    LogInfo("UpdateWorldObject:   Updating panels: No panel details " + DebugWorldObject(wo));
                }
            }
            else
            {
                LogInfo("UpdateWorldObject:   Updating panels: Game object not found of " + DebugWorldObject(wo));
            }
        }

        static void ReceiveMessageSetTransform(MessageSetTransform st)
        {
            if (worldObjectById.TryGetValue(st.id, out var wo))
            {
                if (st.mode != MessageSetTransform.Mode.GameObjectOnly)
                {
                    wo.SetPositionAndRotation(st.position, st.rotation);
                }
                if (st.mode != MessageSetTransform.Mode.WorldObjectOnly
                    && TryGetGameObject(wo, out var go) && go != null)
                {
                    go.transform.position = st.position;
                    go.transform.rotation = st.rotation;
                }
            }
        }

        static void ReceiveMessageSetLinkedGroups(MessageSetLinkedGroups mslg)
        {
            if (worldObjectById.TryGetValue(mslg.id, out var wo))
            {
                if (mslg.groupIds == null || mslg.groupIds.Count == 0)
                {
                    wo.SetLinkedGroups(null);
                }
                else
                {
                    List<Group> groups = new();
                    foreach (var gid in mslg.groupIds)
                    {
                        groups.Add(GroupsHandler.GetGroupViaId(gid));
                    }
                    wo.SetLinkedGroups(groups);
                }
                var openedUi = Managers.GetManager<WindowsHandler>().GetOpenedUi();
                if (openedUi == DataConfig.UiType.GroupSelector)
                {
                    var window = (UiWindowGroupSelector)Managers.GetManager<WindowsHandler>().GetWindowViaUiId(openedUi);
                    var windowWo = (WorldObject)uiWindowGroupSelectorWorldObject.GetValue(window);
                    if (windowWo != null && windowWo.GetId() == mslg.id)
                    {
                        window.SetGroupSelectorWorldObject(wo);
                    }
                }

                var go = wo.GetGameObject();
                if (go != null) {
                    var linkedScreen = go.GetComponentInChildren<ScreenShowLinkedGroup>();
                    if (linkedScreen != null)
                    {
                        linkedScreen.SetGroupSelectedImage(wo);
                    }
                }

                if (updateMode == MultiplayerMode.CoopHost)
                {
                    SendAllClientsExcept(mslg.sender.id, mslg);
                }
            }
        }

        /// <summary>
        /// Fully updates the state of the world object represented by the message.
        /// 
        /// This includes creating the WorldObject itself, creating the GameObject to
        /// place it in the world, changing text, color, panel setup, positioning.
        /// 
        /// In addition, a mockup inventory is also created if the message has a non-zero inventory id.
        /// We'd expect an inventory sync to correctly fill in that inventory.
        /// </summary>
        /// <param name="mwo">The message to use for creating/updating a WorldObject.</param>
        static void UpdateWorldObject(MessageWorldObject mwo)
        {
            bool isNew = false;
            if (!worldObjectById.TryGetValue(mwo.id, out var wo))
            {
                Group gr = GroupsHandler.GetGroupViaId(mwo.groupId);
                if (gr != null)
                {
                    wo = WorldObjectsHandler.CreateNewWorldObject(gr, mwo.id);
                    LogInfo("UpdateWorldObject: Creating new WorldObject " + mwo.id + " - " + mwo.groupId);
                    isNew = true;
                }
                else
                {
                    LogError("UpdateWorldObject:   Unknown group = " + mwo.groupId + " for " + mwo.id);
                    return;
                }
            }
            bool wasPlaced = wo.GetIsPlaced();
            var oldPosition = wo.GetPosition();
            var oldRotation = wo.GetRotation();
            var oldColor = wo.GetColor();
            var oldText = wo.GetText();
            var oldPanelIds = wo.GetPanelsId();
            var oldIId = wo.GetLinkedInventoryId();

            wo.SetPositionAndRotation(mwo.position, mwo.rotation);
            wo.SetColor(mwo.color);
            wo.SetText(mwo.text);
            wo.SetGrowth(mwo.growth);
            wo.SetSetting(mwo.settings);

            wo.SetPanelsId(mwo.panelIds);
            wo.SetDontSaveMe(false);

            List<Group> groups = new List<Group>();
            foreach (var gid in mwo.groupIds)
            {
                groups.Add(GroupsHandler.GetGroupViaId(gid));
            }
            wo.SetLinkedGroups(groups);

            if (mwo.inventoryId > 0)
            {
                wo.SetLinkedInventoryId(mwo.inventoryId);
                Inventory inv = InventoriesHandler.GetInventoryById(mwo.inventoryId);
                if (inv == null)
                {
                    LogInfo("UpdateWorldObject:   Creating default inventory " + mwo.inventoryId
                        + " of WorldObject " + DebugWorldObject(wo));
                    InventoriesHandler.CreateNewInventory(1, mwo.inventoryId);
                }

                if (oldIId != mwo.inventoryId)
                {
                    var wh = Managers.GetManager<WindowsHandler>();
                    if (wh != null && wh.GetOpenedUi() == DataConfig.UiType.Container)
                    {
                        var wc = wh.GetWindowViaUiId(DataConfig.UiType.Container) as UiWindowContainer;
                        if (wc != null)
                        {
                            var ri = uiWindowContainerRightInventory(wc);
                            if (ri != null && ri.GetId() == oldIId)
                            {
                                uiWindowContainerRightInventory(wc) = inv;
                                inv.DisplayIn(wc.containerInventoryContainer, true, true, !WorldObjectsIdHandler.IsWorldObjectFromScene(mwo.id));
                            }
                        }

                    }
                }
            }
            else
            {
                wo.SetLinkedInventoryId(0);
            }

            bool doPlace = wo.GetIsPlaced();
            bool doUpdatePanels = (oldPanelIds == null && mwo.panelIds.Count != 0)
                || (oldPanelIds != null && !oldPanelIds.SequenceEqual(mwo.panelIds));

            bool hasGameObject = TryGetGameObject(wo, out var go) && go != null;

            bool dontUpdatePosition = false;
            if (!wasPlaced && doPlace && !hasGameObject)
            {
                go = WorldObjectsHandler.InstantiateWorldObject(wo, true);
                hasGameObject = true;
                LogInfo("UpdateWorldObject:   Placing GameObject for WorldObject " + DebugWorldObject(wo));
                dontUpdatePosition = true;
            }
            else
            if ((wasPlaced || isNew) && !doPlace)
            {
                if (hasGameObject)
                {
                    LogInfo("UpdateWorldObject:   WorldObject " + wo.GetId() + " GameObject destroyed: not placed");
                    TryRemoveGameObject(wo);
                    Destroy(go);
                    hasGameObject = false;
                }
                rocketsInFlight.Remove(mwo.id);
            }
            if (!doPlace && hasGameObject)
            {
                LogInfo("UpdateWorldObject:   WorldObject " + wo.GetId() + " GameObject destroyed: not placed");
                TryRemoveGameObject(wo);
                Destroy(go);
                hasGameObject = false;
            }

            if (hasGameObject)
            {
                if (doPlace && !dontUpdatePosition)
                {
                    if (IsChanged(oldPosition, mwo.position) || IsChanged(oldRotation, mwo.rotation))
                    {
                        if (!rocketsInFlight.Contains(mwo.id))
                        {
                            var drone = go.GetComponent<DroneSmoother>();
                            if (drone == null || drone.targetReached)
                            {
                                LogInfo("UpdateWorldObject:   Placement " + wo.GetId() + ", " + wo.GetGroup().GetId() + ", position=" + mwo.position + ", rotation=" + mwo.rotation);
                                go.transform.position = mwo.position;
                                go.transform.rotation = mwo.rotation;
                            }
                        }
                    }
                }
                if (!string.Equals(oldText, mwo.text))
                {
                    var wot = go.GetComponentInChildren<WorldObjectText>();
                    if (wot != null)
                    {
                        wot.SetText(mwo.text);
                    }
                }
                if (!IsChanged(oldColor, mwo.color))
                {
                    var woc = go.GetComponentInChildren<WorldObjectColor>();
                    if (woc != null)
                    {
                        woc.SetColor(mwo.color);
                    }
                }
            }
            if (doUpdatePanels)
            {
                LogInfo("UpdateWorldObject:   Panels " + wo.GetId() + ", " + wo.GetGroup().GetId());
                UpdatePanelsOn(wo);
            }

            if (mwo.makeGrabable)
            {
                if (hasGameObject)
                {
                    Destroy(go.GetComponent<ActionMinable>());
                    var grabComponent = go.GetComponent<ActionGrabable>();
                    if (grabComponent == null)
                    {
                        go.AddComponent<ActionGrabable>();
                        LogInfo("UpdateWorldObject:   Add ActionGrabable to " + DebugWorldObject(wo));
                    }
                }
                else
                {
                    LogInfo("UpdateWorldObject:   makeGrabable no GameObject for " + DebugWorldObject(wo));
                }
            }
        }
    }
}
