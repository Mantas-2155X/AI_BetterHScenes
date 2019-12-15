using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

using HarmonyLib;

using BepInEx;
using BepInEx.Logging;
using BepInEx.Harmony;
using BepInEx.Configuration;

using AIProject;
using AIChara;
using AIProject.Definitions;
using Manager;

using UnityEngine;

namespace AI_BetterHScenes
{
    [BepInPlugin(nameof(AI_BetterHScenes), nameof(AI_BetterHScenes), VERSION)][BepInProcess("AI-Syoujyo")]
    public class AI_BetterHScenes : BaseUnityPlugin
    {
        public const string VERSION = "1.3.1";

        private new static ManualLogSource Logger;

        private static bool inHScene;

        private static VirtualCameraController hCamera;

        private static List<ChaControl> shouldCleanUp;
        
        private static GameObject map;
        private static GameObject mapSimulation;
        private static List<SkinnedCollisionHelper> collisionHelpers;
        
        private static bool mapShouldEnable; // compatibility with other plugins which might disable the map
        private static bool mapSimulationShouldEnable; // compatibility with other plugins which might disable the map simulation
        
        private static ConfigEntry<bool> retainCumAfterH { get; set; }
        private static ConfigEntry<bool> keepButtonsInteractive { get; set; }
        private static ConfigEntry<int> hPointSearchRange { get; set; }
        private static ConfigEntry<bool> forceTearsOnWeakness { get; set; }
        private static ConfigEntry<bool> stripMalePantsStartH { get; set; }
        private static ConfigEntry<bool> stripMalePantsChangeAnim { get; set; }
        private static ConfigEntry<bool> unlockCamera { get; set; }
        
        private static ConfigEntry<bool> disableMap { get; set; }
        private static ConfigEntry<bool> disableMapSimulation { get; set; }
        private static ConfigEntry<bool> optimizeCollisionHelpers { get; set; }
        
        private static float Remap(float value, float from1, float to1, float from2, float to2) {
            return (value - from1) / (to1 - from1) * (to2 - from2) + from2;
        }
        
        private void Awake()
        {
            Logger = base.Logger;

            shouldCleanUp = new List<ChaControl>();
            
            retainCumAfterH = Config.Bind("QoL", "Keep cum on body after H", false, new ConfigDescription("Keep cum on body after H, will clean up if taking a bath or changing clothes"));
            keepButtonsInteractive = Config.Bind("QoL", "Keep UI buttons interactive", true, new ConfigDescription("Keep buttons interactive during certain events like orgasm"));
            hPointSearchRange = Config.Bind("QoL", "H point search range", 60, new ConfigDescription("Range in which H points are shown when changing location", new AcceptableValueRange<int>(1, 1000)));
            forceTearsOnWeakness = Config.Bind("QoL", "Tears when weakness is reached", true, new ConfigDescription("Make girl cry when weakness is reached during H"));
            stripMalePantsStartH = Config.Bind("QoL", "Strip male pants on H start", true, new ConfigDescription("Strip male/futa pants when starting H"));
            stripMalePantsChangeAnim = Config.Bind("QoL", "Strip male pants on anim change & start", false, new ConfigDescription("Strip male/futa pants when changing H animation & starting H"));
            unlockCamera = Config.Bind("QoL", "Unlock camera movement", true, new ConfigDescription("Unlock camera zoom out / distance limit during H"));
            
            disableMap = Config.Bind("Performance Improvements", "Disable map", false, new ConfigDescription("Disable map during H scene"));
            disableMapSimulation = Config.Bind("Performance Improvements", "Disable map simulation", false, new ConfigDescription("Disable map simulation during H scene (WARNING: May cause some effects to disappear)"));
            optimizeCollisionHelpers = Config.Bind("Performance Improvements", "Optimize collisionhelpers", true, new ConfigDescription("Optimize collisionhelpers by letting them update once per frame"));

            hPointSearchRange.SettingChanged += delegate
            {
                if (!inHScene)
                    return;
                
                if (Singleton<HSceneSprite>.IsInstance() && Singleton<HSceneSprite>.Instance != null)
                    Singleton<HSceneSprite>.Instance.HpointSearchRange = hPointSearchRange.Value;
            };

            unlockCamera.SettingChanged += delegate
            {
                if (hCamera == null || !inHScene)
                    return;
                
                hCamera.isLimitDir = !unlockCamera.Value;
                hCamera.isLimitPos = !unlockCamera.Value;
            };
            
            var harmony = new Harmony("AI_BetterHScenes_1");

            //-- Strip male/futa pants when starting H --//
            var type_1 = typeof(HScene).GetNestedTypes(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Single(x => x.Name.StartsWith("<StartAnim>c__Iterator1"));
            var method_1 = type_1.GetMethod("MoveNext");
            var postfix_1 = new HarmonyMethod(typeof(AI_BetterHScenes), nameof(HScene_StartAnim_StripMalePants));
            harmony.Patch(method_1, null, postfix_1);
            
            //-- Strip male/futa pants when changing animation --//
            var type_2 = typeof(HScene).GetNestedTypes(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Single(x => x.Name.StartsWith("<ChangeAnimation>c__Iterator2"));
            var method_2 = type_2.GetMethod("MoveNext");
            var postfix_2 = new HarmonyMethod(typeof(AI_BetterHScenes), nameof(HScene_ChangeAnimation_StripMalePants));
            harmony.Patch(method_2, null, postfix_2);

            disableMap.SettingChanged += delegate
            {
                if (map == null || !inHScene)
                    return;
                
                map.SetActive(!disableMap.Value);
                mapShouldEnable = disableMap.Value;
            };
            
            disableMapSimulation.SettingChanged += delegate
            {
                if (mapSimulation == null || !inHScene)
                    return;
                
                mapSimulation.SetActive(!disableMapSimulation.Value);
                mapSimulationShouldEnable = disableMapSimulation.Value;
            };
            
            optimizeCollisionHelpers.SettingChanged += delegate
            {
                if (collisionHelpers == null || !inHScene)
                    return;

                foreach (var helper in collisionHelpers)
                {
                    if (helper == null)
                        return;
                    
                    if (!optimizeCollisionHelpers.Value)
                        helper.forceUpdate = true;
                    
                    helper.updateOncePerFrame = optimizeCollisionHelpers.Value;
                }
            };

            HarmonyWrapper.PatchAll(typeof(AI_BetterHScenes));
        }

        //-- Add character to the shouldCleanUp list --//
        [HarmonyPostfix, HarmonyPatch(typeof(SiruPasteCtrl), "Proc")]
        public static void SiruPasteCtrl_Proc_PopulateList(SiruPasteCtrl __instance)
        {
            if (!inHScene || !retainCumAfterH.Value || shouldCleanUp == null)
                return;

            ChaControl chara = Traverse.Create(__instance).Field("chaFemale").GetValue<ChaControl>();
            if (chara == null || shouldCleanUp.Contains(chara))
                return;

            AgentActor agent = Singleton<Manager.Map>.Instance.AgentTable.Values.FirstOrDefault(actor => actor != null && actor.ChaControl == chara);
            if (agent == null)
                return;
            
            for (int i = 0; i < 5; i++)
            {
                ChaFileDefine.SiruParts parts = (ChaFileDefine.SiruParts)i;
                
                if (chara.GetSiruFlag(parts) == 0) 
                    continue;
                
                shouldCleanUp.Add(chara);
                
                int bathDesireType = Desire.GetDesireKey(Desire.Type.Bath);
                int lewdDesireType = Desire.GetDesireKey(Desire.Type.H);

                float clampedReason = Remap(agent.GetFlavorSkill(FlavorSkill.Type.Reason), 0, 99999f, 0, 100f);
                float clampedDirty = Remap(agent.GetFlavorSkill(FlavorSkill.Type.Dirty), 0, 99999f, 0, 100f);
                float clampedLewd = agent.GetDesire(lewdDesireType) ?? 0;
                float newBathDesire = 100f + clampedReason - clampedDirty - clampedLewd * 1.25f;
                
                agent.SetDesire(bathDesireType, Mathf.Clamp(newBathDesire, 0f, 100f));
                break;
            }
        }
        
        //-- Clean up chara after bath if retaining cum effect --//
        [HarmonyPostfix, HarmonyPatch(typeof(Bath), "OnCompletedStateTask")]
        public static void Bath_OnCompletedStateTask_CleanUp(Bath __instance) => CleanUpSiru(__instance);
        
        //-- Clean up chara after changing if retaining cum effect --//
        [HarmonyPostfix, HarmonyPatch(typeof(ClothChange), "OnCompletedStateTask")]
        public static void ClothChange_OnCompletedStateTask_CleanUp(ClothChange __instance) => CleanUpSiru(__instance);
        
        //-- Retain siru state EndProc--//
        [HarmonyTranspiler, HarmonyPatch(typeof(HScene), "EndProc")]
        public static IEnumerable<CodeInstruction> HScene_EndProc_PreventCleaningSiru(IEnumerable<CodeInstruction> instructions)
        {
            var il = instructions.ToList();

            var index = il.FindIndex(instruction => instruction.opcode == OpCodes.Callvirt && (instruction.operand as MethodInfo)?.Name == "SetSiruFlag");
            if (index <= 0)
            {
                Logger.LogMessage("Failed transpiling 'HScene_EndProc_PreventCleaningSiru' SetSiruFlag index not found!");
                Logger.LogWarning("Failed transpiling 'HScene_EndProc_PreventCleaningSiru' SetSiruFlag index not found!");
                return il;
            }

            il[index - 11] = new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(AI_BetterHScenes), nameof(ChangeSiruIndex)));
            
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
                Logger.LogMessage("Failed transpiling 'HScene_OnDisable_PreventCleaningSiru' SetSiruFlag index not found!");
                Logger.LogWarning("Failed transpiling 'HScene_OnDisable_PreventCleaningSiru' SetSiruFlag index not found!");
                return il;
            }

            il[index - 8] = new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(AI_BetterHScenes), nameof(ChangeSiruIndex)));
            
            return il;
        }

        private static int ChangeSiruIndex()
        {
            return retainCumAfterH.Value ? 5 : 0;
        }
        
        private static void CleanUpSiru(AgentStateAction __instance)
        {
            if (shouldCleanUp == null || shouldCleanUp.Count == 0 || __instance == null || __instance.Owner == null)
                return;
            
            var tree = (__instance.Owner as AgentBehaviorTree);
            if (tree == null)
                return;

            AgentActor agent = tree.SourceAgent;
            if (agent == null || agent.ChaControl == null || !shouldCleanUp.Contains(agent.ChaControl))
                return;

            for (int i = 0; i < 5; i++)
            {
                ChaFileDefine.SiruParts parts = (ChaFileDefine.SiruParts)i;
                agent.ChaControl.SetSiruFlag(parts, 0);
            }

            shouldCleanUp.Remove(agent.ChaControl);
        }

        //-- Strip male/futa pants when starting H --//
        //-- Strip male/futa pants when changing animation --//
        private static void HScene_StartAnim_StripMalePants() => HScene_StripMalePants(stripMalePantsStartH.Value);
        private static void HScene_ChangeAnimation_StripMalePants() => HScene_StripMalePants(stripMalePantsChangeAnim.Value);
        private static void HScene_StripMalePants(bool shouldStrip)
        {
            if (!shouldStrip || !Singleton<HSceneManager>.IsInstance())
                return;

            HSceneManager manager = Singleton<HSceneManager>.Instance;
            if (manager == null || manager.Player == null)
                return;

            ChaControl ply = manager.Player.ChaControl;
            if ((ply.sex == 0 || manager.bFutanari) && ply.IsClothesStateKind(1))
                ply.SetClothesState(1, 2);
        }
       
        //-- Make girl cry if weakness is reached --//
        [HarmonyPrefix, HarmonyPatch(typeof(HVoiceCtrl), "SetFace")]
        public static void HVoiceCtrl_SetFace_ForceTearsOnWeakness(HVoiceCtrl __instance, ref HVoiceCtrl.FaceInfo _face)
        {
            if (!inHScene || !forceTearsOnWeakness.Value || !__instance.ctrlFlag.isFaintness)
                return;

            _face.tear = 1f;
        }
        
        //-- Keep buttons interactive during certain events like orgasm --//
        [HarmonyPrefix, HarmonyPatch(typeof(HSceneSpriteCategories), "Changebuttonactive")]
        public static void HSceneSpriteCategories_Changebuttonactive_KeepButtonsInteractive(ref bool val)
        {
            if (keepButtonsInteractive.Value && !val)
                val = true;
        }
        
        //-- Disable map during H to improve performance --//
        //-- Disable map simulation during H to improve performance --//
        //-- Remove hcamera movement limit --//
        //-- Change H point search range --//
        [HarmonyPostfix, HarmonyPatch(typeof(HScene), "SetStartVoice")]
        public static void HScene_SetStartVoice_DisableMap_UnlockCamera(HScene __instance)
        {
            inHScene = true;
            
            map = GameObject.Find("map00_Beach");
            if (map == null)
                map = GameObject.Find("map_01_data");

            mapSimulation = GameObject.Find("CommonSpace/MapRoot/MapSimulation(Clone)");
            collisionHelpers = new List<SkinnedCollisionHelper>();

            if (hPointSearchRange.Value != 60 && Singleton<HSceneSprite>.IsInstance() && Singleton<HSceneSprite>.Instance != null)
                Singleton<HSceneSprite>.Instance.HpointSearchRange = hPointSearchRange.Value;
            
            if (map != null && disableMap.Value)
            {
                map.SetActive(false);
                mapShouldEnable = true;
            }
            
            if (mapSimulation != null && disableMapSimulation.Value)
            {
                mapSimulation.SetActive(false);
                mapSimulationShouldEnable = true;
            }
            
            HSceneFlagCtrl flagCtrl = __instance.ctrlFlag;
            if (flagCtrl == null || flagCtrl.cameraCtrl == null)
                return;

            hCamera = flagCtrl.cameraCtrl;

            if (!unlockCamera.Value)
                return;
            
            hCamera.isLimitDir = false;
            hCamera.isLimitPos = false;
        }
        
        //-- Enable map after H if disabled previously --//
        //-- Enable map simulation after H if disabled previously --//
        [HarmonyPrefix, HarmonyPatch(typeof(HScene), "EndProc")]
        public static void HScene_EndProc_EnableMap()
        {
            inHScene = false;
            
            if(map != null)
                if (mapShouldEnable)
                {
                    map.SetActive(true);
                    mapShouldEnable = false;
                }
            
            if(mapSimulation != null)
                if (mapSimulationShouldEnable)
                {
                    mapSimulation.SetActive(true);
                    mapSimulationShouldEnable = false;
                }
        }
        
        //-- Fix for the massive FPS drop during HScene insert/service positions --//
        [HarmonyPostfix, HarmonyPatch(typeof(SkinnedCollisionHelper), "Init")]
        public static void SkinnedCollisionHelper_Init_UpdateOncePerFrame(SkinnedCollisionHelper __instance)
        {
            if (!inHScene || __instance == null || collisionHelpers == null)
                return;
            
            collisionHelpers.Add(__instance);
            
            if(optimizeCollisionHelpers.Value)
                __instance.updateOncePerFrame = true;
        }
    }
}
