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
                    toDelete.Add(kv.Key);
                }

                HashSet<int> unplaceSceneObjects = new();
                foreach (MessageWorldObject mwo in mc.worldObjects)
                {
                    //LogInfo("WorldObject " + mwo.id + " - " + mwo.groupId + " at " + mwo.position);
                    toDelete.Remove(mwo.id);

                    UpdateWorldObject(mwo, unplaceSceneObjects);
                }
                DeleteActionMinableForIds(unplaceSceneObjects);

                foreach (int id in toDelete)
                {
                    //LogInfo("WorldObject " + id + " destroyed: " + DebugWorldObject(id));
                    if (worldObjectById.TryGetValue(id, out var wo))
                    {
                        if (TryGetGameObject(wo, out var go))
                        {
                            LogInfo("WorldObject " + id + " GameObject destroyed: no longer exists");
                            UnityEngine.Object.Destroy(go);
                            TryRemoveGameObject(wo);
                        }
                        WorldObjectsHandler.DestroyWorldObject(wo);
                    }
                }
            }
        }

        static void SendWorldObject(WorldObject worldObject, bool makeGrabable)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("UpdateWorldObject|");
            MessageWorldObject.AppendWorldObject(sb, '|', worldObject, makeGrabable);

            LogInfo("Sending> " + sb.ToString());

            sb.Append("\r");
            Send(sb.ToString());
            Signal();
        }

        static void ReceiveMessageUpdateWorldObject(MessageUpdateWorldObject mc)
        {
            LogInfo("ReceiveMessageUpdateWorldObject: " + mc.worldObject.id + ", " + mc.worldObject.groupId);
            UpdateWorldObject(mc.worldObject, null);
        }

        static bool DeleteActionMinableForId(int id)
        {

            foreach (GameObject sceneGo in FindObjectsOfType<GameObject>())
            {
                var uid = sceneGo.GetComponentInChildren<WorldUniqueId>();
                if (uid != null && uid.GetWorldUniqueId() == id)
                {
                    UnityEngine.Object.Destroy(sceneGo);
                    if (worldObjectById.TryGetValue(id, out var wo))
                    {
                        TryRemoveGameObject(wo);
                    }
                    LogInfo("UpdateWorldObject:   GameObject deleted via WorldUniqueId " + id);
                    return true;
                }
            }
            foreach (ActionMinable am in FindObjectsOfType<ActionMinable>()) {
                var woa = am.GetComponent<WorldObjectAssociated>();
                if (woa != null && woa.GetWorldObject().GetId() == id)
                {
                    UnityEngine.Object.Destroy(am.gameObject);
                    if (worldObjectById.TryGetValue(id, out var wo))
                    {
                        TryRemoveGameObject(wo);
                    }
                    LogInfo("UpdateWorldObject:   GameObject deleted via ActionMinable " + id);
                    return true;
                }
            }

            LogWarning("UpdateWorldObject:   WorldUniqueId, ActionMinable not found " + id);
            return false;
        }

        static void DeleteActionMinableForIds(HashSet<int> ids)
        {

            if (ids.Count != 0)
            {
                foreach (GameObject sceneGo in FindObjectsOfType<GameObject>())
                {
                    var uid = sceneGo.GetComponentInChildren<WorldUniqueId>();
                    if (uid != null)
                    {
                        int id = uid.GetWorldUniqueId();
                        if (ids.Contains(uid.GetWorldUniqueId()))
                        {
                            ids.Remove(id);
                            UnityEngine.Object.Destroy(sceneGo);
                            if (worldObjectById.TryGetValue(id, out var wo))
                            {
                                TryRemoveGameObject(wo);
                            }
                            LogInfo("UpdateWorldObject:   GameObject deleted via WorldUniqueId " + id);
                        }
                    }
                }
                if (ids.Count != 0)
                {
                    foreach (ActionMinable am in FindObjectsOfType<ActionMinable>())
                    {
                        var woa = am.GetComponent<WorldObjectAssociated>();
                        if (woa != null)
                        {
                            int id = woa.GetWorldObject().GetId();
                            if (ids.Contains(id))
                            {
                                ids.Remove(id);
                                UnityEngine.Object.Destroy(am.gameObject);
                                if (worldObjectById.TryGetValue(id, out var wo))
                                {
                                    TryRemoveGameObject(wo);
                                }
                                LogInfo("UpdateWorldObject:   GameObject deleted via ActionMinable " + id);
                            }
                        }
                    }
                }

                if (ids.Count != 0)
                {
                    LogWarning("UpdateWorldObject:   WorldUniqueId, ActionMinable not found " + string.Join(", ", ids));
                }
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
        /// <param name="unplaceSceneObjects">Set of identifiers to unplace while loading into a world</param>
        static void UpdateWorldObject(MessageWorldObject mwo, HashSet<int> unplaceSceneObjects)
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

            wo.SetPositionAndRotation(mwo.position, mwo.rotation);
            bool doPlace = wo.GetIsPlaced();
            wo.SetColor(mwo.color);
            wo.SetText(mwo.text);
            wo.SetGrowth(mwo.growth);

            List<int> beforePanelIds = wo.GetPanelsId();
            bool doUpdatePanels = (beforePanelIds == null && mwo.panelIds.Count != 0)
                || (beforePanelIds != null && !beforePanelIds.SequenceEqual(mwo.panelIds));
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
            }
            else
            {
                wo.SetLinkedInventoryId(0);
            }

            bool hasGameObject = TryGetGameObject(wo, out var go) && go != null;

            bool dontUpdatePosition = false;
            if (!wasPlaced && doPlace)
            {
                go = WorldObjectsHandler.InstantiateWorldObject(wo, true);
                hasGameObject = true;
                LogInfo("UpdateWorldObject:   Placing GameObject for WorldObject " + DebugWorldObject(wo));
                dontUpdatePosition = true;
            }
            else
            if (wasPlaced && !doPlace)
            {
                if (hasGameObject)
                {
                    LogInfo("UpdateWorldObject:   WorldObject " + wo.GetId() + " GameObject destroyed: not placed");
                    UnityEngine.Object.Destroy(go);
                    TryRemoveGameObject(wo);
                }
                /*
                else
                {
                    LogInfo("WorldObject " + wo.GetId() + " has no associated GameObject");
                }
                */
                rocketsInFlight.Remove(mwo.id);
            }
            if (doPlace && !dontUpdatePosition && hasGameObject)
            {
                if (IsChanged(oldPosition, mwo.position) || IsChanged(oldRotation, mwo.rotation))
                {
                    if (!rocketsInFlight.Contains(mwo.id))
                    {
                        LogInfo("UpdateWorldObject:   Placement " + wo.GetId() + ", " + wo.GetGroup().GetId() + ", position=" + mwo.position + ", rotation=" + mwo.rotation);
                        go.transform.position = mwo.position;
                        go.transform.rotation = mwo.rotation;
                    }
                }
            }
            if (doUpdatePanels)
            {
                LogInfo("UpdateWorldObject:   Panels " + wo.GetId() + ", " + wo.GetGroup().GetId());
                UpdatePanelsOn(wo);
            }
            if (!string.Equals(oldText, mwo.text) && hasGameObject)
            {
                var wot = go.GetComponentInChildren<WorldObjectText>();
                if (wot != null)
                {
                    wot.SetText(mwo.text);
                }
            }
            if (!IsChanged(oldColor, mwo.color) && hasGameObject)
            {
                var woc = go.GetComponentInChildren<WorldObjectColor>();
                if (woc != null)
                {
                    woc.SetColor(mwo.color);
                }
            }

            if (mwo.makeGrabable)
            {
                if (hasGameObject)
                {
                    UnityEngine.Object.Destroy(go.GetComponent<ActionMinable>());
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

            // remove already mined objects
            if (isNew && WorldObjectsIdHandler.IsWorldObjectFromScene(wo.GetId()) && !doPlace)
            {
                if (unplaceSceneObjects != null)
                {
                    unplaceSceneObjects.Add(wo.GetId());
                }
                else
                {
                    DeleteActionMinableForId(wo.GetId());
                }
            }
        }
    }
}
