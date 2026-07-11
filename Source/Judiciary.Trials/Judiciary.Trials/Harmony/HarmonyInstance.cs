using HarmonyLib;
using System.Reflection;
using Verse;

namespace Broski.Judiciary.Trials
{
    [StaticConstructorOnStartup]
    public static class Main
    {
        static Main()
        {
            var harmony = new Harmony("Broski.Judiciary.Trials");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }
}
