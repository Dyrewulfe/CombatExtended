using Harmony;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;
using System.Reflection.Emit;
using System;


namespace CombatExtended.Harmony.Compatibility
{
    [HarmonyPatch]
    class Harmony_Compat_Zombiefied
    {
        static readonly string logPrefix = Assembly.GetExecutingAssembly().GetName().Name + " :: ";
        static readonly Assembly ass = AppDomain.CurrentDomain.GetAssemblies().
                                SingleOrDefault(assembly => assembly.
                                GetName().Name == "Zombiefied");

        private static bool IsHeadwear(ApparelLayerDef layer)
        {
            return layer.GetModExtension<ApparelLayerExtension>()?.IsHeadwear ?? false;
        }

        internal static bool Prepare()
        {
            if (ass?.FullName.Contains("Zombiefied") ?? false)
            {
                return true;
            }
            return false;
        }

        internal static IEnumerable<MethodBase> TargetMethods()
        {
            var found = false;
            foreach (var t in ass.GetTypes())
            {
                foreach (var m in AccessTools.GetDeclaredMethods(t))
                {
                    if (m.Name.Contains("TryGetGraphicApparel"))
                    {
                        found = true;
                        yield return m;
                    }
                }
            }
            if (found)
            {
                Log.Message($"{logPrefix}Applying compatibility patch for {ass.FullName}");
            }
        }

        internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var drop = false;

            foreach (var code in instructions)
            {
                if (drop)
                {
                    drop = false;
                    continue;
                }

                if (code.opcode == OpCodes.Ldsfld && code.operand == AccessTools.Field(typeof(ApparelLayerDefOf), nameof(ApparelLayerDefOf.Overhead)))
                {
                    drop = true;
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Harmony_ApparelGraphicRecordGetter), nameof(IsHeadwear)));
                }
                else
                {
                    yield return code;
                }
            }
        }
    }
}
