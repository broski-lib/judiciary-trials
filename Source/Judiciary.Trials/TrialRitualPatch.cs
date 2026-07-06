using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.Sound;

namespace Broski.Judiciary.Trials
{
    public static class TrialRitualUtility
    {
        public const int SoftCooldownTicks = 300000;

        private static readonly FieldInfo RitualField = typeof(Command_Ritual).GetField("ritual", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo TargetInfoField = typeof(Command_Ritual).GetField("targetInfo", BindingFlags.NonPublic | BindingFlags.Instance);

        private static readonly AbilityDef AbilityDef = DefDatabase<AbilityDef>.GetNamed("Trial");
        private static readonly CompProperties_AbilityStartTrial Comp = AbilityDef.comps.OfType<CompProperties_AbilityStartTrial>().First();

        public static TargetingParameters TargetParams => AbilityDef.verbProperties.targetParams;
        public static string ConvictRoleId => Comp.targetRoleId;

        public static Precept_Ritual GetRitual(Command_Ritual cmd) => (Precept_Ritual)RitualField.GetValue(cmd);
        public static TargetInfo GetTargetInfo(Command_Ritual cmd) => (TargetInfo)TargetInfoField.GetValue(cmd);

        public static bool IsTrial(Precept_Ritual ritual)
        {
            var def = ritual?.def;
            return def == Comp.ritualDef || def == Comp.ritualDefForPrisoner || def == Comp.ritualDefForMentalState;
        }

        public static bool IsValidConvict(Pawn p)
        {
            if (p == null || p.Dead) return false;
            return AbilityUtility.ValidateCanWalk(p, showMessages: false, ability: null)
                && AbilityUtility.ValidateNotGuilty(p, showMessages: false, ability: null);
        }

        public static bool HasValidConvict(Map map) =>
            map != null && map.mapPawns.AllPawnsSpawned.Any(IsValidConvict);

        public static Precept_Ritual RitualForConvict(Precept_Ritual original, Pawn convict)
        {
            PreceptDef def = null;
            if (convict.InMentalState) def = Comp.ritualDefForMentalState;
            else if (convict.IsPrisonerOfColony) def = Comp.ritualDefForPrisoner;
            if (def == null) return original;
            return original.ideo?.PreceptsListForReading.OfType<Precept_Ritual>().FirstOrDefault(r => r.def == def) ?? original;
        }
    }

    [HarmonyPatch(typeof(Command_Ritual), nameof(Command_Ritual.ProcessInput))]
    public static class Patch_Command_Ritual_ProcessInput
    {
        [HarmonyPrefix]
        public static bool Prefix(Command_Ritual __instance)
        {
            Precept_Ritual ritual = TrialRitualUtility.GetRitual(__instance);
            if (!TrialRitualUtility.IsTrial(ritual)) return true;

            TargetInfo targetInfo = TrialRitualUtility.GetTargetInfo(__instance);
            Find.Targeter.BeginTargeting(
                TrialRitualUtility.TargetParams,
                t =>
                {
                    Pawn pawn = t.Pawn;
                    if (!TrialRitualUtility.IsValidConvict(pawn)) return;
                    Precept_Ritual chosen = TrialRitualUtility.RitualForConvict(ritual, pawn);
                    var forced = new Dictionary<string, Pawn> { { TrialRitualUtility.ConvictRoleId, pawn } };
                    chosen.ShowRitualBeginWindow(targetInfo, null, null, forced);
                    SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                },
                highlightAction: null,
                targetValidator: t => t.Pawn is Pawn p && TrialRitualUtility.IsValidConvict(p));
            return false;
        }
    }

    [HarmonyPatch(typeof(Command_Ritual), "ValidateDisabledState")]
    public static class Patch_Command_Ritual_ValidateDisabledState
    {
        [HarmonyPostfix]
        public static void Postfix(Command_Ritual __instance)
        {
            if (__instance.Disabled) return;
            if (!TrialRitualUtility.IsTrial(TrialRitualUtility.GetRitual(__instance))) return;
            if (!TrialRitualUtility.HasValidConvict(TrialRitualUtility.GetTargetInfo(__instance).Map))
                __instance.Disable("No valid colonists, prisoners, or slaves to accuse.");
        }
    }

    // Soft-cooldown override: trial's repeat-penalty window is 5 days instead of vanilla's 20.
    [HarmonyPatch(typeof(Precept_Ritual), nameof(Precept_Ritual.RepeatPenaltyActive), MethodType.Getter)]
    public static class Patch_RepeatPenaltyActive
    {
        [HarmonyPrefix]
        public static bool Prefix(Precept_Ritual __instance, ref bool __result)
        {
            if (!TrialRitualUtility.IsTrial(__instance)) return true;
            __result = __instance.isAnytime
                && __instance.lastFinishedTick != -1
                && __instance.def.useRepeatPenalty
                && __instance.TicksSinceLastPerformed < TrialRitualUtility.SoftCooldownTicks;
            return false;
        }
    }

    [HarmonyPatch(typeof(Precept_Ritual), nameof(Precept_Ritual.RepeatPenaltyProgress), MethodType.Getter)]
    public static class Patch_RepeatPenaltyProgress
    {
        [HarmonyPrefix]
        public static bool Prefix(Precept_Ritual __instance, ref float __result)
        {
            if (!TrialRitualUtility.IsTrial(__instance)) return true;
            __result = (float)__instance.TicksSinceLastPerformed / TrialRitualUtility.SoftCooldownTicks;
            return false;
        }
    }

    [HarmonyPatch(typeof(Precept_Ritual), nameof(Precept_Ritual.RepeatPenaltyTimeLeft), MethodType.Getter)]
    public static class Patch_RepeatPenaltyTimeLeft
    {
        [HarmonyPrefix]
        public static bool Prefix(Precept_Ritual __instance, ref string __result)
        {
            if (!TrialRitualUtility.IsTrial(__instance)) return true;
            __result = (TrialRitualUtility.SoftCooldownTicks - __instance.TicksSinceLastPerformed).ToStringTicksToPeriod();
            return false;
        }
    }
}
