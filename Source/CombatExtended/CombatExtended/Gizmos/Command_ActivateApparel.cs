using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using UnityEngine;
using Verse.AI;

namespace CombatExtended
{
    public class Command_ActivateApparel : Command_Action
    {
        public CompActiveApparel compApparel;

        bool isActive
        {
            get { return compApparel.isActive; }
        }

        public override GizmoResult GizmoOnGUI(Vector2 loc, float maxWidth)
        {
            GizmoResult result = base.GizmoOnGUI(loc, maxWidth);
            Rect rect = new Rect(loc.x, loc.y, this.GetWidth(maxWidth), 75f);
            Rect position = new Rect(rect.x + rect.width - 24f, rect.y, 24f, 24f);
            Texture2D image = (!this.isActive) ? Widgets.CheckboxOffTex : Widgets.CheckboxOnTex;
            GUI.DrawTexture(position, image);
            return result;
        }
    }
}