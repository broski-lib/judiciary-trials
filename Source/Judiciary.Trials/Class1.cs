using Verse;

namespace Broski.Judiciary.Trials
{
    [StaticConstructorOnStartup]
    public static class ModMain
    {
        static ModMain()
        {
            Log.Message("[MyFirstMod] Hello from Linux! The C# assembly has loaded successfully.");
        }
    }
}
