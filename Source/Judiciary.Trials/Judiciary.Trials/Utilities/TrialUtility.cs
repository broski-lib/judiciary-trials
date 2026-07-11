using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace Broski.Judiciary.Trials
{
    public static class TrialUtility
    {
        private static readonly AbilityDef abilityDef = DefDatabase<AbilityDef>.GetNamed("Trial");

        public static readonly CompProperties_AbilityStartTrial Comp =
            abilityDef.comps.OfType<CompProperties_AbilityStartTrial>().First();

        public static readonly TargetingParameters TargetParams = abilityDef.verbProperties.targetParams;

        public static readonly string ConvictRoleId = Comp.targetRoleId;

        private static readonly HashSet<PreceptDef> trialDefs = new HashSet<PreceptDef>
        {
            Comp.ritualDef,
            Comp.ritualDefForPrisoner,
            Comp.ritualDefForMentalState
        };

        public static bool IsTrial(Precept_Ritual r) =>
            r != null && trialDefs.Contains(r.def);

        // Mirrors the original Trial ability verb: not downed, not guilty.
        public static bool IsValidConvict(Pawn p) =>
            p != null &&
            // p.Dead short-circuits the null-guilt access on a corpse.
            !p.Dead &&
            AbilityUtility.ValidateCanWalk(p, false, null) &&
            AbilityUtility.ValidateNotGuilty(p, false, null);

        public static bool HasValidConvict(Map m) =>
            m != null && m.mapPawns.AllPawnsSpawned.Any(IsValidConvict);

        public static Precept_Ritual RitualFor(Pawn p, Precept_Ritual original)
        {
            PreceptDef preferredDef = p.InMentalState ? Comp.ritualDefForMentalState
                                    : p.IsPrisonerOfColony ? Comp.ritualDefForPrisoner
                                    : null;

            if (preferredDef == null)
            {
                return original;
            }

            return original.ideo?.PreceptsListForReading
                .OfType<Precept_Ritual>()
                .FirstOrDefault(r => r.def == preferredDef)
                ?? original;
        }
    }
}
