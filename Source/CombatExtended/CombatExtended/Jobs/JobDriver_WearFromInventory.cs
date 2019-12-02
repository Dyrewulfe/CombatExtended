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
    public class JobDriver_WearFromInventory : JobDriver
    {
        #region Fields
		private int duration;
		private int unequipBuffer;
		#endregion

		#region Properties
        private Apparel Apparel
        {
            get
            {
                return job.GetTarget(TargetIndex.A).Thing as Apparel;
            }
        }
		#endregion

		#region Methods
		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look<int>(ref this.duration, "duration", 0, false);
			Scribe_Values.Look<int>(ref this.unequipBuffer, "unequipBuffer", 0, false);
		}

		public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        public override void Notify_Starting()
        {
            base.Notify_Starting();
			duration = (int)(Apparel.GetStatValue(StatDefOf.EquipDelay, true) * 60f);
			Apparel apparel = Apparel;
			List<Apparel> wornApparel = pawn.apparel.WornApparel;
			for (int i = wornApparel.Count - 1; i >= 0; i--)
			{
				if (!ApparelUtility.CanWearTogether(apparel.def, wornApparel[i].def, pawn.RaceProps.body))
				{
					duration += (int)(wornApparel[i].GetStatValue(StatDefOf.EquipDelay, true) * 60f);
				}
			}
		}

        protected override IEnumerable<Toil> MakeNewToils()
        {
			Toil prepare = new Toil();
			prepare.tickAction = delegate ()
			{
				unequipBuffer++;
				TryUnequipSomething();
			};
			prepare.WithProgressBarToilDelay(TargetIndex.A, false, -0.5f);
			prepare.defaultCompleteMode = ToilCompleteMode.Delay;
			prepare.defaultDuration = duration;
			yield return prepare;
			yield return Toils_General.Do(delegate
			{
				Apparel apparel = Apparel;
				pawn.inventory.innerContainer.Remove(apparel);
				pawn.apparel.Wear(apparel, true);
				if (pawn.outfits != null && job.playerForced)
				{
					pawn.outfits.forcedHandler.SetForced(apparel, true);
				}
			});
			
			//Continue previous job if possible
			Toil continueToil = new Toil
            {
                defaultCompleteMode = ToilCompleteMode.Instant
            };
            yield return continueToil;
        }

		private void TryUnequipSomething()
		{
			Apparel apparel = Apparel;
			List<Apparel> wornApparel = pawn.apparel.WornApparel;
			foreach (var a in wornApparel)
			{
				Log.Message(a.LabelCap);
			}
			for (int i = wornApparel.Count - 1; i >= 0; i--)
			{
				if (!ApparelUtility.CanWearTogether(apparel.def, wornApparel[i].def, pawn.RaceProps.body))
				{
					int num = (int)(wornApparel[i].GetStatValue(StatDefOf.EquipDelay, true) * 60f);
					if (unequipBuffer >= num)
					{
						bool forbid = pawn.Faction != null && pawn.Faction.HostileTo(Faction.OfPlayer);
						Apparel apparel2;
						int count = 0;
						var inv = pawn.TryGetComp<CompInventory>();
						//TODO:ActivatedEquipment - Make sure this calculates properly
						if (inv?.CanFitInInventory(wornApparel[i], out count, false, true) ?? false)
						{
							if (inv.container.TryAddOrTransfer(wornApparel[i]))
							{
								inv.UpdateInventory();
								return;
							}
						}
						if (!pawn.apparel.TryDrop(wornApparel[i], out apparel2, pawn.PositionHeld, forbid))
						{
							Log.Error(pawn + " could not drop " + wornApparel[i].ToStringSafe(), false);
							EndJobWith(JobCondition.Errored);
							return;
						}
					}
					break;
				}
			}
		}
        #endregion
    }
}

