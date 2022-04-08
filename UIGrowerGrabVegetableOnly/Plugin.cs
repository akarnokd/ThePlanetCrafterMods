using BepInEx;
using SpaceCraft;
using HarmonyLib;
using UnityEngine;
using MijuTools;
using UnityEngine.InputSystem;

namespace UIGrowerGrabVegetableOnly
{
    [BepInPlugin("akarnokd.theplanetcraftermods.uigrowergrabvegetableonly", "(UI) Grower Grab Vegetable Only", "1.0.0.0")]
    public class Plugin : BaseUnityPlugin
    {

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(MachineGrower), "OnVegetableGrabed")]
        static bool MachineGrower_OnVegetableGrabed(float ___chanceOfGettingSeedsOnHundred, Inventory ___inventory, GameObject ___instantiatedGameObject)
        {
            // Unfortunately, this has to be patched mid-method so I grabbed the full original source

            UnityEngine.Object.Destroy(___instantiatedGameObject);
            WorldObject worldObject = ___inventory.GetInsideWorldObjects()[0];
            ___inventory.RemoveItem(worldObject, false);
            if (___chanceOfGettingSeedsOnHundred < (float)UnityEngine.Random.Range(0, 100))
            {
                WorldObjectsHandler.DestroyWorldObject(worldObject);
                return false;
            }
            PlayerMainController activePlayerController = Managers.GetManager<PlayersManager>().GetActivePlayerController();
            worldObject.SetLockInInventoryTime(0f);
            // -------------------------------------------------
            if (Keyboard.current[Key.LeftShift].isPressed)
            {
                ___inventory.AddItem(worldObject);
                return false;
            }
            // -------------------------------------------------
            if (activePlayerController.GetPlayerBackpack().GetInventory().AddItem(worldObject))
            {
                Managers.GetManager<DisplayersHandler>().GetInformationsDisplayer()
                    .AddInformation(2.5f, Readable.GetGroupName(worldObject.GetGroup()), DataConfig.UiInformationsType.InInventory, worldObject.GetGroup().GetImage());
                return false;
            }
            WorldObjectsHandler.DropOnFloor(worldObject, activePlayerController.GetAimController().GetAimRay().GetPoint(1f), 0f);

            return false;
        }
    }
}
