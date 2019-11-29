using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Linq;
using System.Text;
using RimWorld;
using Verse;
using UnityEngine;
using Harmony;
using CombatExtended;

namespace CombatExtended.Harmony
{
    [HarmonyPatch(typeof(JobGiver_OptimizeApparel), "TryGiveJob")]
    static class HarmonyJobGiver_OptimizeApparel
    {

        internal static IEnumerable<CodeInstruction> Transpiler(ILGenerator gen, IEnumerable<CodeInstruction> instructions, MethodBase origin)
        {
            // For the sake of readable patches
            ConstructorInfo newJob = AccessTools.Constructor(typeof(Verse.AI.Job), new Type[] { typeof(JobDef), typeof(LocalTargetInfo), typeof(LocalTargetInfo) });

            MethodInfo isInAnyStorage = AccessTools.Method(typeof(StoreUtility), nameof(StoreUtility.IsInAnyStorage));
            MethodInfo thingsInGroup = AccessTools.Method(typeof(ListerThings), nameof(ListerThings.ThingsInGroup));
            MethodInfo prependInventory = AccessTools.Method(typeof(HarmonyJobGiver_OptimizeApparel), nameof(PrependInventory));
            MethodInfo inventoryContains = AccessTools.Method(typeof(Pawn_InventoryTracker), nameof(Pawn_InventoryTracker.Contains));
            MethodInfo castFromThingImplicit = AccessTools.Method(typeof(LocalTargetInfo), "op_Implicit", new Type[] { typeof(Thing) });

            FieldInfo wear = AccessTools.Field(typeof(JobDefOf), nameof(JobDefOf.Wear));
            FieldInfo inventory = AccessTools.Field(typeof(Pawn), nameof(Pawn.inventory));
            FieldInfo wearFromInventory = AccessTools.Field(typeof(CE_JobDefOf), nameof(CE_JobDefOf.WearFromInventory));

            int locApparelIndex = origin.GetMethodBody().LocalVariables.FirstOrDefault(l => l.LocalType == typeof(Apparel)).LocalIndex;
            int locThingIndex = origin.GetMethodBody().LocalVariables.FirstOrDefault(l => l.LocalType == typeof(Thing)).LocalIndex;

            /* Target:
             *  List<Thing> list = pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.Apparel);
             * 
             * We're prepending this list with all apparel from the inventory
             * See List<Thing> PrependInventory(List<Thing>, Pawn)
             */
            List<CodeInstruction> patch0 = new List<CodeInstruction>()
            {
                new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(OpCodes.Call, prependInventory),
            };

            /* Target:
             *  Apparel apparel = (Apparel)list[j];
             *	if (currentOutfit.filter.Allows(apparel))
             *	{
             *		if (apparel.IsInAnyStorage())
             */
            List<CodeInstruction> patch1 = new List<CodeInstruction>()
            {
                // if (apparel.IsInAnyStorage() || pawn.inventory.Contains(apparel))
                new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(OpCodes.Ldfld, inventory),
                new CodeInstruction(OpCodes.Ldloc, locApparelIndex),
                new CodeInstruction(OpCodes.Callvirt, inventoryContains),
                new CodeInstruction(OpCodes.Or),
            };

            /* Target:
             *  if (thing == null)
             *  {
             *      this.SetNextOptimizeTick(pawn);
             *      return null;
             *  }
             *  return new Job(JobDefOf.Wear, thing);
             *  
             * Result:
             *  if (thing == null)
             *  {
             *      this.SetNextOptimizeTick(pawn);
             *      return null;
             *  }
             *  if (pawn.inventory.Contains(thing)
             *  {
             *      return new Job(CE_JobDefOf.WearFromInventory, pawn, thing);
             *  }
             *  return new Job(JobDefOf.Wear, thing);
             *  
             */

            const int p2_brfalse_index = 4;
            // unless someone else is playing around in here, this will actually be label31 in the transpiled output
            Label label31 = gen.DefineLabel();
            List<CodeInstruction> patch2 = new List<CodeInstruction>()
            {
                // if (pawn.inventory.Contains(thing)
                new CodeInstruction(OpCodes.Ldarg_1) { labels = new List<Label> { label31 } },
                new CodeInstruction(OpCodes.Ldfld, inventory),
                new CodeInstruction(OpCodes.Ldloc, locThingIndex),
                new CodeInstruction(OpCodes.Callvirt, inventoryContains),
                // branch to "return new Job(JobDefOf.Wear, thing) on fail, label captured during patching
                new CodeInstruction(OpCodes.Brfalse, null),
                // return new Job(CE_JobDefOf.WearFromInventory, pawn, thing);
                new CodeInstruction(OpCodes.Ldsfld, wearFromInventory),
                new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(OpCodes.Call, castFromThingImplicit),
                new CodeInstruction(OpCodes.Ldloc, locThingIndex),
                new CodeInstruction(OpCodes.Call, castFromThingImplicit),
                new CodeInstruction(OpCodes.Newobj, newJob),
                new CodeInstruction(OpCodes.Ret),
            };

            CodeInstruction prevCode = new CodeInstruction(OpCodes.Nop);
            bool[] patched = new bool[3];
            foreach (var code in instructions)
            {
                if (!patched[2])
                {
                    if (patched[1]
                        && prevCode.opcode == OpCodes.Ldloc_S
                        && code.opcode == OpCodes.Brtrue)
                    {
                        code.operand = label31;
                    }
                    if (code.opcode == OpCodes.Ldsfld && code.operand == wear)
                    {
                        patch2[p2_brfalse_index].operand = code.labels.FirstOrDefault();
                        foreach (var p in patch2)
                        {
                            yield return p;
                        }
                        patched[2] = true;
                    }
                }
                yield return code;
                if (!patched[0] && code.opcode == OpCodes.Callvirt && code.operand == thingsInGroup)
                {
                    foreach (var p in patch0)
                    {
                        yield return p;
                    }
                    patched[0] = true;
                }
                if (!patched[1] && code.opcode == OpCodes.Call && code.operand == isInAnyStorage)
                {
                    foreach (var p in patch1)
                    {
                        yield return p;
                    }
                    patched[1] = true;
                }
                prevCode = code;
            }
        }

        internal static List<Thing> PrependInventory(List<Thing> things, Pawn pawn)
        {
            var list = pawn.inventory.innerContainer.Where(t => t is Apparel).Concat(things).ToList();
            foreach (var thing in list)
            {
                Log.Message(thing.LabelCap);
            }
            return list;
        }
    }
}
