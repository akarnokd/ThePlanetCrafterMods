using BepInEx;
using MijuTools;
using SpaceCraft;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace UIShowContainerInfo
{
    [BepInPlugin("akarnokd.theplanetcraftermods.uishowcontainerinfo", "(UI) Show Container Content Info", "1.0.0.0")]
    public class Plugin : BaseUnityPlugin
    {

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(ActionOpenable), nameof(ActionOpenable.OnHover))]
        static bool ActionOpenable_OnHover(ActionOpenable __instance, BaseHudHandler ___hudHandler)
        {
            string custom = "";
            WorldObjectText woText = __instance.GetComponent<WorldObjectText>();
            if (woText != null && woText.GetTextIsSet())
            {
                custom = " \"" + woText.GetText() + "\" ";
            }
            string text = Readable.GetGroupName(Components.GetComponentOnGameObjectOrInParent<WorldObjectAssociated>(__instance.gameObject).GetWorldObject().GetGroup());
            InventoryAssociated componentOnGameObjectOrInParent = Components.GetComponentOnGameObjectOrInParent<InventoryAssociated>(__instance.gameObject);
            if (componentOnGameObjectOrInParent != null)
            {
                Inventory inventory = componentOnGameObjectOrInParent.GetInventory();
                int count = inventory.GetInsideWorldObjects().Count;
                int size = inventory.GetSize();
                text = string.Concat(new object[]
                {
                    text,
                    custom,
                    "   [  ",
                    count,
                    "  /  ",
                    size,
                    "  ]  "
                });
                if (count >= size)
                {
                    text += "  --- FULL ---  ";
                }
                if (count > 0)
                {
                    text += inventory.GetInsideWorldObjects()[0].GetGroup().GetId();
                }
            }
            ___hudHandler.DisplayCursorText("UI_Open", 0f, text);

            // base.OnHover() => Actionable.OnHover()
            ActionnableInteractive ai = __instance.GetComponent<ActionnableInteractive>();
            if (ai != null)
            {
                ai.OnHoverInteractive();
            }
            // this.HandleHoverMaterial(true, null);
            System.Reflection.MethodInfo mi = AccessTools.Method(typeof(Actionnable), "HandleHoverMaterial", new System.Type[] { typeof(bool), typeof(GameObject) });
            mi.Invoke(__instance, new object[] { true, null });

            return false;
        }
    }
}
