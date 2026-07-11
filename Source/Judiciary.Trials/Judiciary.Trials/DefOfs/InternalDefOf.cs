using RimWorld;
using Verse;

namespace Broski.Judiciary.Trials
{
    [DefOf]
    public static class InternalDefOf
    {
        static InternalDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(InternalDefOf));
        }

        // Custom ability cooldown group for trial rituals.
        public static AbilityGroupDef JT_TrialCooldown;
    }
}
