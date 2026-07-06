using Verse;
using HarmonyLib;

namespace Broski.Judiciary.Trials
{
    [StaticConstructorOnStartup]
    public static class ModMain
    {
        static ModMain()
        {
            var harmony = new Harmony("Judiciary.Trials");

            harmony.PatchAll();

            Log.Message("[Judiciary] successfully patched");
        }
    }
}
