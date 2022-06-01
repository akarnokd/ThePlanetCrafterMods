using BepInEx;
using HarmonyLib;
using MijuTools;
using SpaceCraft;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace FeatMultiplayer
{
    public partial class Plugin : BaseUnityPlugin
    {
        /// <summary>
        /// The vanilla game uses linear lookup for its <code>WorldObjectsHandler.GetWorldObjectViaId()</code>
        /// which is slow for big worlds. This is a shadow map of all world objects by their id
        /// and is maintained by patching the relevant methods that manipulated the underlying list
        /// of <code>WorldObjectsHandler.allWorldObjects</code>.
        /// </summary>
        static readonly Dictionary<int, WorldObject> worldObjectById = new();

        /// <summary>
        /// The map from WorldObjects to GameObjects.
        /// Obtained via reflection from the private field <code>WorldObjectsHandler.worldObjects</code>.
        /// <code>WorldObjectsHandler.GetGameObjectViaWorldObject</code> crashes if
        /// the WorldObject is not in the map. We need the dictionary to run TryGetValue/ContainsKey on it.
        /// </summary>
        static Dictionary<WorldObject, GameObject> gameObjectByWorldObject;

        /// <summary>
        /// Try locating the GameObject for the given WorldObject.
        /// </summary>
        /// <param name="wo">The world object to use as a reference</param>
        /// <param name="go">The GameObject if found</param>
        /// <returns>true if found</returns>
        internal static bool TryGetGameObject(WorldObject wo, out GameObject go)
        {
            return gameObjectByWorldObject.TryGetValue(wo, out go);
        }

        /// <summary>
        /// Tries to remove the GameObject associated with the given WorldObject.
        /// </summary>
        /// <param name="wo">The WorldObject whose GameObject should be removed.</param>
        /// <returns>True if successful.</returns>
        internal static bool TryRemoveGameObject(WorldObject wo)
        {
            return gameObjectByWorldObject.Remove(wo);
        }

        /// <summary>
        /// The vanilla game uses this method to store the WorldObject it created.
        /// We also track it via the Dictionary for faster id-based lookups.
        /// </summary>
        /// <param name="_worldObject">The WorldObject to store.</param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(WorldObjectsHandler), "StoreNewWorldObject")]
        static void WorldObjectsHandler_StoreNewWorldObject(WorldObject _worldObject)
        {
            worldObjectById[_worldObject.GetId()] = _worldObject;
        }

        /// <summary>
        /// The vanilla game uses this method to forget WorldObjects, assuming they
        /// don't represent something from the scene, such as pre-placed resources
        /// or chests (their id is 10xxxxxxxx).
        /// We also remove them from our Dictionary.
        /// Note that this doesn't destroy the associated GameObject!
        /// </summary>
        /// <param name="_worldObject">The world object to be destroyed</param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(WorldObjectsHandler), nameof(WorldObjectsHandler.DestroyWorldObject))]
        static void WorldObjectsHandler_DestroyWorldObject(WorldObject _worldObject)
        {
            var id = _worldObject.GetId();
            if (worldObjectById.ContainsKey(id) &&
                !WorldObjectsIdHandler.IsWorldObjectFromScene(id))
            {
                worldObjectById.Remove(id);
            }
        }

        /// <summary>
        /// The vanilla game uses linear lookup for finding WorldObjects.
        /// Since we have an id to WorldObject Dictionary, let's use it.
        /// Should the game work much faster overall with bigger worlds.
        /// </summary>
        /// <param name="_id">The id to find</param>
        /// <param name="__result">The found WorldObject or null if not found.</param>
        /// <returns>True if found.</returns>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(WorldObjectsHandler), nameof(WorldObjectsHandler.GetWorldObjectViaId))]
        static bool WorldObjectsHandler_GetWorldObjectViaId(int _id, ref WorldObject __result)
        {
            worldObjectById.TryGetValue(_id, out __result);
            return false;
        }

        /// <summary>
        /// The vanilla game updates all world objects at once upon loading a save.
        /// We also redo the full id to WorldObject Dictionary.
        /// </summary>
        /// <param name="_allWorldObjects"></param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(WorldObjectsHandler), nameof(WorldObjectsHandler.SetAllWorldObjects))]
        static void WorldObjectsHandler_SetAllWorldObjects(List<WorldObject> _allWorldObjects)
        {
            worldObjectById.Clear();
            foreach (WorldObject wo in _allWorldObjects)
            {
                worldObjectById[wo.GetId()] = wo;
            }
        }

        /// <summary>
        /// Find the current players <see cref="PlayerMainController"/>.
        /// Caching this value is not recommended as it can go away.
        /// </summary>
        /// <returns>The current controller or null if not active.</returns>
        internal static PlayerMainController GetPlayerMainController()
        {
            PlayersManager p = Managers.GetManager<PlayersManager>();
            if (p != null)
            {
                return p.GetActivePlayerController();
            }
            return null;
        }

        /// <summary>
        /// Override the ID generator on the client to generate ids starting with 30.
        /// This will allow make a distinction of Host-created (20xxxxxxxx) and
        /// client created objects.
        /// </summary>
        /// <param name="__result">Override the result.</param>
        /// <returns>false for the client, true for the host</returns>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(WorldObjectsIdHandler), nameof(WorldObjectsIdHandler.GetNewWorldObjectIdForDb))]
        static bool WorldObjectsIdHandler_GetNewWorldObjectIdForDb(ref int __result)
        {
            if (updateMode == MultiplayerMode.CoopClient)
            {
                int randomId = 300000000 + UnityEngine.Random.Range(1000000, 9999999);
                int max = -1;
                bool duplicate = false;
                foreach (WorldObject wo in WorldObjectsHandler.GetAllWorldObjects())
                {
                    int id = wo.GetId();
                    if (id == randomId)
                    {
                        duplicate = true;
                    }
                    max = Math.Max(max, id);
                }
                if (duplicate)
                {
                    __result = max + 1;
                }
                else
                {
                    __result = randomId;
                }
                return false;
            }
            return true;
        }

        /// <summary>
        /// Dictionary to find an Inventory by its id in constant time.
        /// </summary>
        static readonly Dictionary<int, Inventory> inventoryById = new();

        /// <summary>
        /// Track the largest known inventory id so a new inventory can be +1.
        /// The vanilla game searches for the max id every time but I
        /// don't see any reason to not use a monotonic id sequence.
        /// Plus, reusing the same id may result in awkward sync issues.
        /// </summary>
        static int maxInventoryId;

        /// <summary>
        /// The vanilla game uses InventoriesHandler::GetInventoryById to find an Inventory, but
        /// uses linear lookup on InventoriesHandler.allWorldInventories.
        /// 
        /// We introduced the <see cref="inventoryById"/> Dictionary to make this lookup
        /// constant time.
        /// </summary>
        /// <param name="id">The inventory id being searched.</param>
        /// <param name="__result">The inventory found or null if not found.</param>
        /// <returns>Always false, prevent the original from running altogether.</returns>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(InventoriesHandler), nameof(InventoriesHandler.GetInventoryById))]
        static bool InventoriesHandler_GetInventoryById(int _inventoryId, ref Inventory __result)
        {
            inventoryById.TryGetValue(_inventoryId, out __result);
            return false;
        }

        /// <summary>
        /// The vanilla game uses InventoriesHandler::CreateNewInventory to create a new inventory,
        /// with an existing or auto-generated identifier.
        /// 
        /// We override this behavior to use the <see cref="maxInventoryId"/> for auto-generating
        /// the inventory id (monotonously) and register the new inventory with the
        /// <see cref="inventoryById"/> Dictionary.
        /// Of course, we then update the <see cref="maxInventoryId"/> to the latest biggest value.
        /// 
        /// The vanilla had no measures for handling duplicate ids so we don't do that here either.
        /// </summary>
        /// <param name="_size">The size of the inventory.</param>
        /// <param name="_inventoryId">If non-zero, the inventory will have this id, if zero, <see cref="maxInventoryId"/> + 1 will be used.</param>
        /// <param name="__result">The inventory instance created.</param>
        /// <param name="___allWorldInventories">The list of known inventories to be added to</param>
        /// <returns>Always false, prevent the original from running altogether.</returns>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(InventoriesHandler), nameof(InventoriesHandler.CreateNewInventory))]
        static bool InventoriesHandler_CreateNewInventory(int _size, int _inventoryId, ref Inventory __result, List<Inventory> ___allWorldInventories)
        {
            int iid = _inventoryId;
            if (iid == 0)
            {
                iid = maxInventoryId + 1;
                maxInventoryId = Math.Max(maxInventoryId, iid);
            }
            else
            if (iid < 9999999)
            {
                // pre-placed objects use inventory ids >= 10_000_000
                // we ignore them for the max id so we don't enter their region
                // with new inventories
                maxInventoryId = Math.Max(maxInventoryId, iid);
            }

            var inv = new Inventory(iid, _size);

            inventoryById[iid] = inv;
            ___allWorldInventories.Add(inv);

            __result = inv;
            return false;
        }

        /// <summary>
        /// The vanilla game uses InventoriesHandler::DestroyInventory to remove an inventory from the game
        /// and destroy its contents as well.
        /// 
        /// We override this so we can remove the inventory from our <see cref="inventoryById"/>.
        /// Also we use a descending indexed linear lookup to find the inventory inside 
        /// InventoriesHandler.allWorldInventories, no way around that, and then call
        /// List::RemoveAt instead of List::Remove, which latter would search for the same 
        /// inventory entry again and do the removal.
        /// </summary>
        /// <param name="_inventoryId">The inventory id to be destroyed</param>
        /// <param name="___allWorldInventories">The list of known inventory instances.</param>
        /// <returns>Always false, prevent the original from running altogether.</returns>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(InventoriesHandler), nameof(InventoriesHandler.DestroyInventory))]
        static bool InventoriesHandler_DestroyInventory(int _inventoryId, List<Inventory> ___allWorldInventories)
        {
            for (int i = ___allWorldInventories.Count - 1; i >= 0; i--)
            {
                Inventory inv = ___allWorldInventories[i];
                int iid = inv.GetId();
                if (iid == _inventoryId)
                {
                    WorldObjectsHandler.DestroyWorldObjects(inv.GetInsideWorldObjects());

                    ___allWorldInventories.RemoveAt(i);
                    inventoryById.Remove(iid);
                    break;
                }
            }
            return false;
        }

        /// <summary>
        /// The vanilla game calls InventoriesHandler::SetAllWorldInventories when a save is loaded and
        /// sets the internal InventoriesHandler.allWorldInventories.
        /// 
        /// We need to intercept this and build up the <see cref="inventoryById"/> Dictionary, as well
        /// as finding the highest inventory id of them.
        /// </summary>
        /// <param name="___allWorldInventories">The list of all known world inventories just set by the vanilla method.</param>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(InventoriesHandler), nameof(InventoriesHandler.SetAllWorldInventories))]
        static void InventoriesHandler_SetAllWorldInventories(List<Inventory> ___allWorldInventories)
        {
            inventoryById.Clear();
            maxInventoryId = 0;
            foreach (Inventory inv in ___allWorldInventories)
            {
                int iid = inv.GetId();
                inventoryById[iid] = inv;

                if (iid < 9999999)
                {
                    // pre-placed objects use inventory ids >= 10_000_000
                    // we ignore them for the max id so we don't enter their region
                    // with new inventories
                    maxInventoryId = Math.Max(maxInventoryId, iid);
                }
            }
        }
    }
}
