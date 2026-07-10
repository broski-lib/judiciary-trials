using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Broski.Judiciary.Trials
{
    public class TrialRitualExtension : DefModExtension
    {
        // 12 in-game hours by default; vanilla repeat penalty is 20 days.
        public int cooldownTicks = 30000;
    }

    public static class TrialUtility
    {
        public const int DefaultRimWorldRepeatPenaltyTicks = 1200000;

        private static readonly AbilityDef AbilityDef = DefDatabase<AbilityDef>.GetNamed("Trial");

        public static readonly CompProperties_AbilityStartTrial Comp =
            AbilityDef.comps.OfType<CompProperties_AbilityStartTrial>().First();

        public static readonly TargetingParameters TargetParams = AbilityDef.verbProperties.targetParams;

        public static readonly string ConvictRoleId = Comp.targetRoleId;

        private static readonly HashSet<PreceptDef> TrialDefs = new HashSet<PreceptDef>
        {
            Comp.ritualDef,
            Comp.ritualDefForPrisoner,
            Comp.ritualDefForMentalState
        };

        public static bool IsTrial(Precept_Ritual r) =>
            r != null && TrialDefs.Contains(r.def);

        // Mirrors the original Trial ability verb: not downed, not guilty.
        public static bool IsValidConvict(Pawn p) =>
            p != null
            // p.Dead short-circuits the null-guilt access on a corpse.
            && !p.Dead
            && AbilityUtility.ValidateCanWalk(p, false, null)
            && AbilityUtility.ValidateNotGuilty(p, false, null);

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

        public static int CooldownTicks(PreceptDef def) =>
            def.GetModExtension<TrialRitualExtension>()?.cooldownTicks
            ?? DefaultRimWorldRepeatPenaltyTicks;

        public static bool UsesVanillaCooldown(PreceptDef def) =>
            CooldownTicks(def) == DefaultRimWorldRepeatPenaltyTicks;
    }

    public class Command_TrialRitual : Command_Ritual
    {
        private readonly Precept_Ritual ritualRef;
        private readonly TargetInfo targetRef;

        public Command_TrialRitual(Precept_Ritual r, TargetInfo t) : base(r, t)
        {
            ritualRef = r;
            targetRef = t;
        }

        public override void ProcessInput(Event ev)
        {
            Find.Targeter.BeginTargeting(
                TrialUtility.TargetParams,
                t =>
                {
                    Pawn p = t.Pawn;
                    if (p == null || !TrialUtility.IsValidConvict(p))
                    {
                        return;
                    }

                    Precept_Ritual chosen = TrialUtility.RitualFor(p, ritualRef);
                    var forced = new Dictionary<string, Pawn>
                    {
                        { TrialUtility.ConvictRoleId, p }
                    };

                    chosen.ShowRitualBeginWindow(targetRef, null, null, forced);
                    SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                },
                null,
                t => t.Pawn is Pawn p && TrialUtility.IsValidConvict(p));
        }

        protected override GizmoResult GizmoOnGUIInt(Rect butRect, GizmoRenderParms parms)
        {
            if (!Disabled && !TrialUtility.HasValidConvict(targetRef.Map))
            {
                Disable("No valid colonists, prisoners, or slaves to accuse.");
            }

            return base.GizmoOnGUIInt(butRect, parms);
        }
    }

    [HarmonyPatch(typeof(Precept_Ritual), nameof(Precept_Ritual.GetGizmoFor))]
    public static class Patch_GetGizmoFor
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

    [HarmonyPatch(typeof(Precept_Ritual), nameof(Precept_Ritual.RepeatPenaltyActive), MethodType.Getter)]
    public static class Patch_RepeatPenaltyActive
    {
        [HarmonyPrefix]
        public static bool Prefix(Precept_Ritual __instance, ref bool __result)
        {
            if (TrialUtility.UsesVanillaCooldown(__instance.def))
            {
                return true;
            }

            int cd = TrialUtility.CooldownTicks(__instance.def);
            __result = __instance.isAnytime
                    && __instance.lastFinishedTick != -1
                    && __instance.def.useRepeatPenalty
                    && __instance.TicksSinceLastPerformed < cd;

            return false;
        }
    }

    [HarmonyPatch(typeof(Precept_Ritual), nameof(Precept_Ritual.RepeatPenaltyProgress), MethodType.Getter)]
    public static class Patch_RepeatPenaltyProgress
    {
        [HarmonyPrefix]
        public static bool Prefix(Precept_Ritual __instance, ref float __result)
        {
            if (TrialUtility.UsesVanillaCooldown(__instance.def))
            {
                return true;
            }

            int cd = TrialUtility.CooldownTicks(__instance.def);
            __result = (float)__instance.TicksSinceLastPerformed / cd;
            return false;
        }
    }

    [HarmonyPatch(typeof(Precept_Ritual), nameof(Precept_Ritual.RepeatPenaltyTimeLeft), MethodType.Getter)]
    public static class Patch_RepeatPenaltyTimeLeft
    {
        [HarmonyPrefix]
        public static bool Prefix(Precept_Ritual __instance, ref string __result)
        {
            if (TrialUtility.UsesVanillaCooldown(__instance.def))
            {
                return true;
            }

            int cd = TrialUtility.CooldownTicks(__instance.def);
            __result = (cd - __instance.TicksSinceLastPerformed).ToStringTicksToPeriod();
            return false;
        }
    }

    // Command_Ritual.DrawIcon bakes the 20-day repeat penalty into the
    // bar fill and the "PeriodDays" label without going through the
    // RepeatPenaltyProgress / TimeLeft getters, so the prefix patches
    // above don't reach the on-gizmo overlay. Replace both 1200000
    // operands with CooldownTicks(ritual.def) - a no-op for rituals
    // that don't carry a TrialRitualExtension.
    [HarmonyPatch(typeof(Command_Ritual), nameof(Command_Ritual.DrawIcon))]
    public static class Patch_DrawIcon
    {
        private static readonly MethodInfo CooldownTicksMethod =
            AccessTools.Method(typeof(TrialUtility), nameof(TrialUtility.CooldownTicks));

        private static readonly FieldInfo RitualField =
            AccessTools.Field(typeof(Command_Ritual), "ritual");

        private static readonly MethodInfo DefGetter =
            AccessTools.PropertyGetter(typeof(Precept_Ritual), "def");

        [HarmonyTranspiler]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (CodeInstruction instr in instructions)
            {
                if (instr.opcode == OpCodes.Ldc_R4 && instr.OperandIs(1200000f))
                {
                    // float operand: this.ritual.def.CooldownTicks() as float
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, RitualField) { labels = instr.labels };
                    yield return new CodeInstruction(OpCodes.Callvirt, DefGetter);
                    yield return new CodeInstruction(OpCodes.Call, CooldownTicksMethod);
                    yield return new CodeInstruction(OpCodes.Conv_R4);
                    continue;
                }

                if (instr.opcode == OpCodes.Ldc_I4 && instr.OperandIs(1200000))
                {
                    // int operand: this.ritual.def.CooldownTicks()
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Ldfld, RitualField) { labels = instr.labels };
                    yield return new CodeInstruction(OpCodes.Callvirt, DefGetter);
                    yield return new CodeInstruction(OpCodes.Call, CooldownTicksMethod);
                    continue;
                }

                yield return instr;
            }
        }
    }
}
