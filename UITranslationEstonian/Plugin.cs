using BepInEx;

namespace UITranslationEstonian
{
    [BepInPlugin("akarnokd.theplanetcraftermods.uitranslationestonian", "(UI) Estonian Translation", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        void Awake()
        {
            LibCommon.UITranslator.AddLanguage("estonian", "labels-et.txt", this, Logger, Config);
        }
    }
}
