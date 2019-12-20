using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;

using HarmonyLib;

namespace AI_BetterHScenes
{
    public static class Transpilers
    {
        //-- Retain siru state EndProc--//
        [HarmonyTranspiler, HarmonyPatch(typeof(HScene), "EndProc")]
        public static IEnumerable<CodeInstruction> HScene_EndProc_PreventCleaningSiru(IEnumerable<CodeInstruction> instructions)
        {
            var il = instructions.ToList();

            var index = il.FindIndex(instruction => instruction.opcode == OpCodes.Callvirt && (instruction.operand as MethodInfo)?.Name == "SetSiruFlag");
            if (index <= 0)
            {
                AI_BetterHScenes.Logger.LogMessage("Failed transpiling 'HScene_EndProc_PreventCleaningSiru' SetSiruFlag index not found!");
                AI_BetterHScenes.Logger.LogWarning("Failed transpiling 'HScene_EndProc_PreventCleaningSiru' SetSiruFlag index not found!");
                return il;
            }

            il[index - 11] = new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Tools), nameof(Tools.ChangeSiruIndex)));
            
            return il;
        }
        
        //-- Retain siru state OnDisable--//
        [HarmonyTranspiler, HarmonyPatch(typeof(HScene), "OnDisable")]
        public static IEnumerable<CodeInstruction> HScene_OnDisable_PreventCleaningSiru(IEnumerable<CodeInstruction> instructions)
        {
            var il = instructions.ToList();

            var index = il.FindIndex(instruction => instruction.opcode == OpCodes.Callvirt && (instruction.operand as MethodInfo)?.Name == "SetSiruFlag");
            if (index <= 0)
            {
                AI_BetterHScenes.Logger.LogMessage("Failed transpiling 'HScene_OnDisable_PreventCleaningSiru' SetSiruFlag index not found!");
                AI_BetterHScenes.Logger.LogWarning("Failed transpiling 'HScene_OnDisable_PreventCleaningSiru' SetSiruFlag index not found!");
                return il;
            }

            il[index - 8] = new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Tools), nameof(Tools.ChangeSiruIndex)));
            
            return il;
        }

    }
}