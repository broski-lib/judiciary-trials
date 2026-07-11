using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace Broski.Judiciary.Trials
{
    // Replaces the stock Command_Ritual on trial PreceptDefs with
    // Command_TrialRitual, which prompts the player to pick a convict pawn
    // (only valid colonists/prisoners/slaves) and routes to the matching
    // Trial/TrialPrisoner/TrialMentalState ritual via TrialUtility.RitualFor.
    // Non-trial rituals pass through untouched.
    [HarmonyPatch(typeof(Precept_Ritual), nameof(Precept_Ritual.GetGizmoFor))]
    public static class PatchPreceptRitualGetGizmoFor
    {
        [HarmonyPostfix]
        public static IEnumerable<Gizmo> Postfix(
            IEnumerable<Gizmo> __result,
            Precept_Ritual __instance,
            TargetInfo t)
        {
            if (!TrialUtility.IsTrial(__instance))
            {
                foreach (Gizmo g in __result)
                {
                    yield return g;
                }
                yield break;
            }

            foreach (Gizmo g in __result)
            {
                if (g is Command_Ritual cr)
                {
                    yield return new Command_TrialRitual(__instance, t)
                    {
                        Disabled = cr.Disabled,
                        disabledReason = cr.disabledReason
                    };
                }
                else
                {
                    yield return g;
                }
            }
        }
    }
}
