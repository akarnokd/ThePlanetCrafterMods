using BepInEx;

namespace UITranslationKorean
{
    [BepInPlugin("akarnokd.theplanetcraftermods.uitranslationkorean", "(UI) Korean Translation", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        void Awake()
        {
            LibCommon.UITranslator.AddLanguage("korean", "labels-ko.txt", this, Logger, Config);
        }
    }
}
