using BepInEx;

namespace UITranslationPolish
{
    [BepInPlugin("akarnokd.theplanetcraftermods.uitranslationpolish", "(UI) Polish Translation", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        void Awake()
        {
            LibCommon.UITranslator.AddLanguage("polish", "labels-pl.txt", this, Logger, Config);
        }
    }
}
