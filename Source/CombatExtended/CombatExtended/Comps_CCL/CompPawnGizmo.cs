using System.Collections.Generic;
using Verse;
using RimWorld;

namespace CombatExtended
{
    public class CompPawnGizmo : ThingComp
    {
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            var pawn = parent as Pawn;
            var equip = pawn != null
                ? pawn.equipment.Primary
                : null;
            var apparel = pawn != null
                ? pawn.apparel.WornApparel
                : null;

            if (
                (equip != null) &&
                (!equip.AllComps.NullOrEmpty())
            )
            {
                foreach (var comp in equip.AllComps)
                {
                    var gizmoGiver = comp as CompRangedGizmoGiver;
                    if (
                        (gizmoGiver != null) &&
                        (gizmoGiver.isRangedGiver)
                    )
                    {
                        foreach (var gizmo in gizmoGiver.CompGetGizmosExtra())
                        {
                            yield return gizmo;
                        }
                    }
                }
            }
            if (apparel != null)
            {
                // This smells damp, I hate it
                foreach (var thing in apparel)
                {
                    if (!thing.AllComps.NullOrEmpty())
                    {
                        foreach (var comp in thing.AllComps)
                        {
                            var gizmoGiver = comp as CompApparelGizmoGiver;
                            if (
                                (gizmoGiver != null) &&
                                (gizmoGiver.isApparelGiver)
                            )
                            {
                                foreach (var gizmo in gizmoGiver.CompGetGizmosExtra())
                                {
                                    yield return gizmo;
                                }
                            }
                        }
                    }
                }
            }
        }

    }

}
