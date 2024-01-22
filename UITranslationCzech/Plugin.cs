using BepInEx;

namespace UITranslationCzech
{
    [BepInPlugin("akarnokd.theplanetcraftermods.uitranslationczech", "(UI) Czech Translation", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        void Awake()
        {
            LibCommon.UITranslator.AddLanguage("czech", "labels-cz.txt", this, Logger, Config);
        }
    }
}
