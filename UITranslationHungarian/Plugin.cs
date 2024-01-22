using BepInEx;

namespace UITranslationHungarian
{
    [BepInPlugin("akarnokd.theplanetcraftermods.uitranslationhungarian", "(UI) Hungarian Translation", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        void Awake()
        {
            LibCommon.UITranslator.AddLanguage("hungarian", "labels-hu.txt", this, Logger, Config);
        }
    }
}
