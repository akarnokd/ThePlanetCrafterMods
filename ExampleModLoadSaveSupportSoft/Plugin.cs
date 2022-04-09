using BepInEx;
using UnityEngine;
using BepInEx.Bootstrap;
using System.Reflection;
using System;

namespace ExampleModLoadSaveSupportSoft
{
    [BepInPlugin(guid, "(Example) Soft Dependency on ModLoadSaveSupport", "1.0.0.0")]
    [BepInDependency(libModLoadSaveSupportGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        const string libModLoadSaveSupportGuid = "akarnokd.theplanetcraftermods.libmodloadsavesupport";
        const string guid = "akarnokd.theplanetcraftermods.examplemodloadsavesupportsoft";

        private IDisposable handle;

        private void Awake()
        {
            // Plugin startup logic
            Logger.LogInfo($"Plugin is loaded!");

            // Locate the libModLoadSaveSupport plugin
            if (Chainloader.PluginInfos.TryGetValue(libModLoadSaveSupportGuid, out BepInEx.PluginInfo pi))
            {
                // locate its RegisterLoadSave method
                MethodInfo mi = pi.Instance.GetType().GetMethod("RegisterLoadSave",
                    new Type[] { typeof(string), typeof(Action<string>), typeof(Func<string>) });

                // call it with our guid and the delegates to our load and save methods
                handle = (IDisposable)mi.Invoke(pi.Instance, new object[] { guid, new Action<string>(OnLoad), new Func<string>(OnSave) });

                Logger.LogInfo("Successfully registered with " + libModLoadSaveSupportGuid);
            } else
            {
                Logger.LogInfo("Could not find " + libModLoadSaveSupportGuid);
            }
        }

        void OnDestroy()
        {
            handle?.Dispose();
            handle = null;
        }

        void OnLoad(string content)
        {
            Logger.LogInfo("Executing OnLoad");
            Logger.LogInfo(content);
        }

        string OnSave()
        {
            Logger.LogInfo("Executing OnSave");
            return "ExampleModLoadSaveSupportSoft example content";
        }

    }
}
