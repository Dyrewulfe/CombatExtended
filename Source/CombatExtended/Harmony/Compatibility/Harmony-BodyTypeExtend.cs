using Harmony;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Verse;
using System.Reflection.Emit;
using System;

// Yes, this really is just running the Harmony_ApparelGraphicRecordGetter transpiler again on another method.

namespace CombatExtended.Harmony.Compatibility
{
    [HarmonyPatch]
    class Harmony_Compat_BodyTypeExtend
    {
        static readonly string logPrefix = Assembly.GetExecutingAssembly().GetName().Name + " :: ";
        static Assembly ass = AppDomain.CurrentDomain.GetAssemblies().
                                SingleOrDefault(assembly => assembly.
                                GetName().Name == "BodyTypeExtend");

        private static bool IsHeadwear(ApparelLayerDef layer)
        {
            return layer.GetModExtension<ApparelLayerExtension>()?.IsHeadwear ?? false;
        }

        static bool Prepare()
        {
            if (ass?.FullName.Contains("BodyTypeExtend") ?? false)
            {
                return true;
            }
            return false;
        }

        static IEnumerable<MethodBase> TargetMethods()
        {
            var found = false;
            foreach (var t in ass.GetTypes())
            {
                foreach (var m in AccessTools.GetDeclaredMethods(t))
                {
                    if (m.Name.Contains("TryGetGraphicApparel_Prefix"))
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
            var write = false;

            foreach (var code in instructions)
            {
                if (write)
                {
                    write = false;
                    code.opcode = OpCodes.Brfalse;
                }

                if (code.opcode == OpCodes.Ldsfld && code.operand == AccessTools.Field(typeof(ApparelLayerDefOf), nameof(ApparelLayerDefOf.Overhead)))
                {
                    write = true;
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
