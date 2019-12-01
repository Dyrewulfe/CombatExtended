﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace CombatExtended
{
    public class CompActiveApparel : CompApparelGizmoGiver
    {
        #region Fields
        private bool active = false;
        #endregion

        #region Properties

        public bool isActive
        {
            get { return active; }
        }

        public CompProperties_ActiveApparel Props
        {
            get
            {
                return (CompProperties_ActiveApparel)props;
            }
        }

        public Apparel Apparel
        {
            get { return parent as Apparel; }
        }

        public Pawn Wearer
        {
            get
            {
                return Apparel?.Wearer;
            }
        }

        public bool ShouldThrowMote => Props.throwMote;
        #endregion

        #region Methods

        public void ToggleApparel()
        {
            active = !active;    
        }

        public void TryToggleApparel()
        {
            Job toggleJob = new Job(CE_JobDefOf.ToggleActiveApparel, Wearer, parent);
            toggleJob.playerForced = true;
            Wearer.jobs.StartJob(toggleJob, JobCondition.InterruptForced, null, Wearer.CurJob?.def != toggleJob.def, true);
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
           
            if (Wearer != null && Wearer.Faction == Faction.OfPlayer)
            {
                Action action = null;
                if (Wearer != null) action = TryToggleApparel;

                // Check for teaching opportunities
                string tag = null;
                //LessonAutoActivator.TeachOpportunity(ConceptDef.Named(tag), turret, OpportunityType.GoodToKnow);

                Command_ActivateApparel apparelCommandGizmo = new Command_ActivateApparel
                {
                    compApparel = this,
                    action = action,
                    defaultLabel = Apparel.def.LabelCap,
                    defaultDesc = "TODO:ActivatedEquipment",
                    icon = Apparel.def.IconTexture(),
                    tutorTag = tag
                };
                yield return apparelCommandGizmo;
            }
        }
        #endregion
    }
}