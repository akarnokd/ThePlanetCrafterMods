using BepInEx;

namespace MiscDebug
{
    [BepInPlugin("akarnokd.theplanetcraftermods.miscdebug", "(Misc) Debug", PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {

        void Awake()
        {
            LibCommon.BepInExLoggerFix.ApplyFix();
        }
    }
}
