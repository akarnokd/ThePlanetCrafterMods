using BepInEx;
using MijuTools;
using BepInEx.Configuration;
using SpaceCraft;
using UnityEngine.InputSystem;
using HarmonyLib;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using UnityEngine;
using System;
using TMPro;
using BepInEx.Logging;

namespace UITeleporterScroll
{
    [BepInPlugin("akarnokd.theplanetcraftermods.uiteleporterscroll", "(UI) Teleporter Scroll Targets", "1.0.0.0")]
    public class Plugin : BaseUnityPlugin
    {

        static int index;
        static List<string> lines;
        static Action<int> callback;
        static GameObject grid;
        static GameObject template;
        static ManualLogSource logger;

        static ConfigEntry<int> maxTargets;

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            maxTargets = Config.Bind("General", "MaxTargets", 6, "Maximum number of targets to show at once.");
            logger = Logger;

            Harmony.CreateAndPatchAll(typeof(Plugin));
        }

        void Update()
        {
            if (lines != null)
            {
                if (Mouse.current.scroll.ReadValue().y < 0)
                {
                    OnDownClicked(null);
                }
                else if(Mouse.current.scroll.ReadValue().y > 0)
                {
                    OnUpClicked(null);
                }


            }
        }

        static void RenderPartialList()
        {
            GameObjects.DestroyAllChildren(grid, false);

            if (lines.Count > maxTargets.Value)
            {
                GameObject up = CreateEntry(grid, index > 0 ? "/\\" : "");
                EventsHelpers.AddTriggerEvent(up, EventTriggerType.PointerClick,
                    new Action<EventTriggerCallbackData>(OnUpClicked), null, null, 0);
            }

            int max = Math.Min(index + maxTargets.Value, lines.Count);
            for (int i = index; i < max; i++)
            {
                GameObject gameObject = CreateEntry(grid, (i + 1) + " - " + lines[i]);
                EventsHelpers.AddTriggerEvent(gameObject, EventTriggerType.PointerClick,
                    new Action<EventTriggerCallbackData>(OnItemClicked), null, null, i);
            }

            if (lines.Count > maxTargets.Value) { 
                GameObject down = CreateEntry(grid, max < lines.Count ? "\\/" : "");
                EventsHelpers.AddTriggerEvent(down, EventTriggerType.PointerClick,
                new Action<EventTriggerCallbackData>(OnDownClicked), null, null, 0);
            }
        }

        static GameObject CreateEntry(GameObject grid, string text)
        {
            var gameObject = UnityEngine.Object.Instantiate<GameObject>(template);
            gameObject.transform.SetParent(grid.transform);
            gameObject.SetActive(true);
            gameObject.GetComponent<RectTransform>().localScale = new Vector3(1f, 1f, 1f);
            gameObject.GetComponentInChildren<TextMeshProUGUI>().text = text;
            return gameObject; 
        }
        [HarmonyPrefix]
        [HarmonyPatch(typeof(UiClickList), nameof(UiClickList.SetValues))]
        static bool UiClickList_SetValues(
            UiClickList __instance,
            GameObject ___grid, 
            ref Action<int> ___callback, 
            GameObject ___itemTemplate,
            List<string> _stringsValues,
            Action<int> _callback)
        {
            __instance.ClearList();
            ___callback = _callback;

            callback = _callback;
            grid = ___grid;
            template = ___itemTemplate;
            index = 0;
            lines = _stringsValues;
            RenderPartialList();
            return false;
        }

        static void OnItemClicked(EventTriggerCallbackData _eventTriggerCallbackData)
        {
            int intValue = _eventTriggerCallbackData.intValue;
            Action<int> cb = callback;

            lines = null;
            callback = null;
            grid = null;
            template = null;
            index = 0;

            cb(intValue);

        }
        static void OnUpClicked(EventTriggerCallbackData _eventTriggerCallbackData)
        {
            int i = index;
            index = Math.Max(0, index - 1);
            logger.LogInfo("Scroll Up " + i + " -> " + index);
            RenderPartialList();
        }
        static void OnDownClicked(EventTriggerCallbackData _eventTriggerCallbackData)
        {
            int i = index;
            index = Math.Min(lines.Count - maxTargets.Value, index + 1);
            logger.LogInfo("Scroll Down " + i + " -> " + index);
            RenderPartialList();
        }
    }
}
