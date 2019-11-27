using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using Verse.AI;
using UnityEngine;

namespace CombatExtended
{
    public class JobDriver_ToggleActiveApparel : JobDriver
    {
        #region Fields
        private CompActiveApparel _compApparel = null;
        #endregion

        #region Properties
        // TargetA == Pawn wearer
        private TargetIndex indWearer => TargetIndex.A;
        private Pawn wearer => TargetThingA as Pawn;
        // TargetB == ThingWithComps (Apparel)
        private TargetIndex indApparel => TargetIndex.B;
        private ThingWithComps apparel => TargetThingB as ThingWithComps; //intentionally non-caching.

        private CompActiveApparel compApparel
        {
            get
            {
                if (_compApparel == null) _compApparel = apparel.TryGetComp<CompActiveApparel>();
                return _compApparel;
            }
        }
        #endregion

        #region Methods
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(indWearer);
            this.FailOnMentalState(indWearer);
            this.FailOnDestroyedOrNull(indApparel);

            // Throw mote
            if (compApparel.ShouldThrowMote && wearer.Map != null)     //holder.Map is temporarily null after game load, skip mote if a pawn was reloading when game was saved
            {
                // TODO:ActiveApparel - Language key for mote
                MoteMaker.ThrowText(pawn.Position.ToVector3Shifted(), wearer.Map, string.Format("Readying ", apparel.def.LabelCap));
            }

            //Toil of do-nothing		
            Toil waitToil = new Toil() { actor = pawn }; // actor was always null in testing...
            waitToil.initAction = () => waitToil.actor.pather.StopDead();
            waitToil.defaultCompleteMode = ToilCompleteMode.Delay;
            waitToil.defaultDuration = Mathf.CeilToInt(compApparel.Props.activationTime.SecondsToTicks());
            yield return waitToil.WithProgressBarToilDelay(indWearer);

            //Actual reloader
            Toil toggleToil = new Toil();
            toggleToil.AddFinishAction(() => compApparel.ToggleApparel());
            yield return toggleToil;

            //Continue previous job if possible
            Toil continueToil = new Toil
            {
                defaultCompleteMode = ToilCompleteMode.Instant
            };
            yield return continueToil;
        }

        public override string GetReport()
        {
            // TODO:ActiveApparel - implement properly
            return "Toggling some damn apparel, yo.";
        }
        #endregion
    }
}