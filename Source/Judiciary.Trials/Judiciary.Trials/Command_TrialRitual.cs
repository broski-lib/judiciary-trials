using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Broski.Judiciary.Trials
{
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
}
