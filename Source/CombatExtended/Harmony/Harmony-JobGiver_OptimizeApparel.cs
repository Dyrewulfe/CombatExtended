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
    static class Harmony_JobGiver_OptimizeApparel
    {

        static readonly string logPrefix = Assembly.GetExecutingAssembly().GetName().Name + " :: " + typeof(Harmony_JobGiver_OptimizeApparel).Name + " :: ";
        static bool success = true;
        static bool label_set = false;
        static bool[] patched = new bool[3];

        private struct Patch
        {
            List<CodeInstruction> codes;
            
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase origin)
        {
            // For the sake of readable patches
            ConstructorInfo newJob = AccessTools.Constructor(typeof(Verse.AI.Job), new Type[] { typeof(JobDef), typeof(LocalTargetInfo), typeof(LocalTargetInfo) });

            MethodInfo isInAnyStorage           = AccessTools.Method(typeof(StoreUtility), nameof(StoreUtility.IsInAnyStorage));
            MethodInfo thingsInGroup            = AccessTools.Method(typeof(ListerThings), nameof(ListerThings.ThingsInGroup));
            MethodInfo prependInventory         = AccessTools.Method(typeof(Harmony_JobGiver_OptimizeApparel), nameof(PrependInventory));
            MethodInfo inventory_Contains       = AccessTools.Method(typeof(Pawn_InventoryTracker), nameof(Pawn_InventoryTracker.Contains));
            MethodInfo castFromThingImplicit    = AccessTools.Method(typeof(LocalTargetInfo), "op_Implicit", new Type[] { typeof(Thing) });

            FieldInfo jobDefOfWear      = AccessTools.Field(typeof(JobDefOf), nameof(JobDefOf.Wear));
            FieldInfo pawn_Inventory    = AccessTools.Field(typeof(Pawn), nameof(Pawn.inventory));
            FieldInfo wearFromInventory = AccessTools.Field(typeof(CE_JobDefOf), nameof(CE_JobDefOf.WearFromInventory));

            int locApparelIndex = origin.GetMethodBody().LocalVariables.FirstOrDefault(l => l.LocalType == typeof(Apparel)).LocalIndex;
            int locThingIndex   = origin.GetMethodBody().LocalVariables.FirstOrDefault(l => l.LocalType == typeof(Thing)).LocalIndex;

            List<List<CodeInstruction>> patches = new List<List<CodeInstruction>>(3);

            /* Target:
             *  List<Thing> list = pawn.Map.listerThings.ThingsInGroup(ThingRequestGroup.Apparel);
             * 
             * We're prepending this list with all apparel from the inventory
             * See List<Thing> PrependInventory(List<Thing>, Pawn)
             */
            patches.Add( new List<CodeInstruction>()
            {
                new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(OpCodes.Call, prependInventory),
            });
            
            /* Target:
             *  Apparel apparel = (Apparel)list[j];
             *	if (currentOutfit.filter.Allows(apparel))
             *	{
             *		if (apparel.IsInAnyStorage())
             */
            patches.Add( new List<CodeInstruction>()
            {
                // if (apparel.IsInAnyStorage() || pawn.inventory.Contains(apparel))
                new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(OpCodes.Ldfld, pawn_Inventory),
                new CodeInstruction(OpCodes.Ldloc, locApparelIndex),
                new CodeInstruction(OpCodes.Callvirt, inventory_Contains),
                new CodeInstruction(OpCodes.Or),
            });

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
             *  if (pawn.inventory.Contains(thing))
             *  {
             *      return new Job(CE_JobDefOf.WearFromInventory, pawn, thing);
             *  }
             *  return new Job(JobDefOf.Wear, thing);
             *  
             */

            // unless someone else is playing around in here, this will actually be label31 in the transpiler debug output
            Label label31 = new Label();
            const int p2_brfalse_index = 4;
            patches.Add( new List<CodeInstruction>()
            {
                // if (pawn.inventory.Contains(thing)
                new CodeInstruction(OpCodes.Ldarg_1) { labels = new List<Label> { label31 } },
                new CodeInstruction(OpCodes.Ldfld, pawn_Inventory),
                new CodeInstruction(OpCodes.Ldloc, locThingIndex),
                new CodeInstruction(OpCodes.Callvirt, inventory_Contains),
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
            });

            CodeInstruction prevCode = new CodeInstruction(OpCodes.Nop);
            List<CodeInstruction> transpiled = new List<CodeInstruction>();
            foreach (var code in instructions)
            {
                if (!patched[2])
                {
                    if (patched[1] 
                        && !label_set
                        && prevCode.opcode == OpCodes.Ldloc_S && (prevCode.operand as LocalBuilder)?.LocalType == typeof(Thing)
                        && code.opcode == OpCodes.Brtrue)
                    {
                        label_set = true;
                        // Creating a new instruction to avoid modifying the original set
                        transpiled.Add(new CodeInstruction(OpCodes.Brtrue, label31));
                        continue;
                    }
                    if (code.opcode == OpCodes.Ldsfld && code.operand == jobDefOfWear)
                    {
                        patches[2][p2_brfalse_index].operand = code.labels.FirstOrDefault();
                        transpiled.AddRange(patches[2]);
                        patched[2] = true;
                    }
                }

                transpiled.Add(code);

                if (!patched[0] && code.opcode == OpCodes.Callvirt && code.operand == thingsInGroup)
                {
                    transpiled.AddRange(patches[0]);
                    patched[0] = true;
                }
                if (!patched[1] && code.opcode == OpCodes.Call && code.operand == isInAnyStorage)
                {
                    transpiled.AddRange(patches[1]);
                    patched[1] = true;
                }
                prevCode = code;
            }

            label_set = false;
            if (patched.All(v => v) && label_set)
            {
                return transpiled;
            }
            else
            {
                success = false;
                return instructions;
            }
        }

        [HarmonyCleanup]
        private static void Cleanup(HarmonyInstance inst, MethodBase origin)
        {
            if (!success)
            {
                StringBuilder report = new StringBuilder($"{logPrefix}An error occured while attempting to apply patches, aborting.", 4096).AppendLine()
                    .AppendLine("If you would like to report this issue, please click this message and copy the information from the window below.")
                    .AppendLine()
                    .AppendLine("Status:")
                    .AppendFormat("    Label set: {0}", label_set).AppendLine()
                    .AppendFormat("    Patch 0:   {0}", patched[0]).AppendLine()
                    .AppendFormat("    Patch 1:   {0}", patched[1]).AppendLine()
                    .AppendFormat("    Patch 2:   {0}", patched[2]).AppendLine()
                    .AppendLine();


                if (inst.GetPatchInfo(origin).Transpilers.Count > 1)
                {
                    report.AppendLine("Possible conflicting patches:");
                    foreach (var p in inst.GetPatchInfo(origin).Transpilers)
                    {
                        // Obviously not a conflict, moving on
                        if (p.owner == inst.Id)
                            continue;

                        report.AppendFormat("{0}::{1}", p.owner, p.patch.Name).AppendLine();
                    }
                    report.AppendLine();
                }
                report.AppendLine("(-------------------------------- Information from this point on is not needed. --------------------------------)");
                Log.Error(report.ToString());
            }
        }

        private static List<Thing> PrependInventory(List<Thing> things, Pawn pawn)
        {
            var list = pawn.inventory.innerContainer.Where(t => t is Apparel).Concat(things).ToList();
            //TODO:ActivatedEquipment - Remove when finalizing
            foreach (var thing in list)
            {
                Log.Message(thing.LabelCap);
            }
            return list;
        }
    }
}
