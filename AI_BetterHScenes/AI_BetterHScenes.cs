using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using HarmonyLib;

using BepInEx;
using BepInEx.Logging;
using BepInEx.Harmony;
using BepInEx.Configuration;

using AIProject;
using AIProject.Definitions;

using AIChara;
using Manager;
using Illusion.Extensions;

using UnityEngine;

namespace AI_BetterHScenes
{
    [BepInPlugin(nameof(AI_BetterHScenes), nameof(AI_BetterHScenes), VERSION)][BepInProcess("AI-Syoujyo")]
    public class AI_BetterHScenes : BaseUnityPlugin
    {
        public const string VERSION = "2.0.1";

        public new static ManualLogSource Logger;

        private static bool inHScene;

        public static HScene hScene;
        public static HSceneManager manager;
        public static HSceneFlagCtrl hFlagCtrl;
        public static HSceneSprite hSprite;
        
        public static VirtualCameraController hCamera;

        public static readonly RaycastHit[] hits = new RaycastHit[15];
        
        public static List<DraggerComponent> draggers;
        public static List<ChaControl> characters;
        public static List<ChaControl> shouldCleanUp;

        public static GameObject map;
        public static GameObject mapSimulation;
        public static List<SkinnedCollisionHelper> collisionHelpers;

        public static bool cameraShouldLock; // compatibility with other plugins which might disable the camera control
        private static bool mapShouldEnable; // compatibility with other plugins which might disable the map
        private static bool mapSimulationShouldEnable; // compatibility with other plugins which might disable the map simulation
        
        private static ConfigEntry<KeyboardShortcut> showMaleDraggers { get; set; }
        private static ConfigEntry<KeyboardShortcut> showFemaleDraggers { get; set; }
        
        private static ConfigEntry<bool> alwaysGaugesHeart { get; set; }
        private static ConfigEntry<bool> positionDraggers { get; set; }
        public static ConfigEntry<bool> cleanMerchantCumAfterH { get; private set; }
        public static ConfigEntry<bool> retainCumAfterH { get; private set; }
        public static ConfigEntry<bool> keepButtonsInteractive { get; private set; }
        private static ConfigEntry<int> hPointSearchRange { get; set; }
        private static ConfigEntry<bool> forceCloseEyesOnWeakness { get; set; }
        private static ConfigEntry<bool> forceTearsOnWeakness { get; set; }
        private static ConfigEntry<bool> stripMalePantsStartH { get; set; }
        private static ConfigEntry<bool> stripMalePantsChangeAnim { get; set; }
        private static ConfigEntry<bool> unlockCamera { get; set; }
        
        private static ConfigEntry<bool> disableMap { get; set; }
        private static ConfigEntry<bool> disableMapSimulation { get; set; }
        private static ConfigEntry<bool> optimizeCollisionHelpers { get; set; }
        
        private void Awake()
        {
            Logger = base.Logger;

            shouldCleanUp = new List<ChaControl>();
            
            positionDraggers = Config.Bind("QoL", "Enable character draggers", false, new ConfigDescription("Enable character position draggers, shown by keys"));
            showMaleDraggers = Config.Bind("QoL", "Show male draggers", new KeyboardShortcut(KeyCode.N));
            showFemaleDraggers = Config.Bind("QoL", "Show female draggers", new KeyboardShortcut(KeyCode.M));
            
            alwaysGaugesHeart = Config.Bind("QoL", "Always hit gauge heart", false, new ConfigDescription("Always hit gauge heart. Will cause girl progress to increase without having to scroll specific amount"));
            cleanMerchantCumAfterH = Config.Bind("QoL", "Clean merchant cum on body after H", false, new ConfigDescription("Clean merchant cum on body after H. Only effective if 'keep cum on body after H' is enabled"));
            retainCumAfterH = Config.Bind("QoL", "Keep cum on body after H", false, new ConfigDescription("Keep cum on body after H, will clean up if taking a bath or changing clothes (if not merchant)"));
            keepButtonsInteractive = Config.Bind("QoL", "Keep UI buttons interactive", false, new ConfigDescription("Keep buttons interactive during certain events like orgasm"));
            hPointSearchRange = Config.Bind("QoL", "H point search range", 60, new ConfigDescription("Range in which H points are shown when changing location (default 60)", new AcceptableValueRange<int>(1, 1000)));
            forceTearsOnWeakness = Config.Bind("QoL", "Tears when weakness is reached", true, new ConfigDescription("Make girl cry when weakness is reached during H"));
            forceCloseEyesOnWeakness = Config.Bind("QoL", "Close eyes when weakness is reached", false, new ConfigDescription("Close girl eyes when weakness is reached during H"));
            stripMalePantsStartH = Config.Bind("QoL", "Strip male pants on H start", true, new ConfigDescription("Strip male/futa pants when starting H"));
            stripMalePantsChangeAnim = Config.Bind("QoL", "Strip male pants on anim change & start", false, new ConfigDescription("Strip male/futa pants when changing H animation & starting H"));
            unlockCamera = Config.Bind("QoL", "Unlock camera movement", true, new ConfigDescription("Unlock camera zoom out / distance limit during H"));
            
            disableMap = Config.Bind("Performance Improvements", "Disable map", false, new ConfigDescription("Disable map during H scene"));
            disableMapSimulation = Config.Bind("Performance Improvements", "Disable map simulation", false, new ConfigDescription("Disable map simulation during H scene (WARNING: May cause some effects to disappear)"));
            optimizeCollisionHelpers = Config.Bind("Performance Improvements", "Optimize collisionhelpers", true, new ConfigDescription("Optimize collisionhelpers by letting them update once per frame"));

            hPointSearchRange.SettingChanged += delegate
            {
                if (!inHScene || hSprite == null)
                    return;

                hSprite.HpointSearchRange = hPointSearchRange.Value;
            };

            unlockCamera.SettingChanged += delegate
            {
                if (!inHScene || hCamera == null)
                    return;
                
                hCamera.isLimitDir = !unlockCamera.Value;
                hCamera.isLimitPos = !unlockCamera.Value;
            };

            disableMap.SettingChanged += delegate
            {
                if (!inHScene || map == null)
                    return;
                
                map.SetActive(!disableMap.Value);
                mapShouldEnable = disableMap.Value;
            };
            
            disableMapSimulation.SettingChanged += delegate
            {
                if (!inHScene || mapSimulation == null)
                    return;
                
                mapSimulation.SetActive(!disableMapSimulation.Value);
                mapSimulationShouldEnable = disableMapSimulation.Value;
            };
            
            optimizeCollisionHelpers.SettingChanged += delegate
            {
                if (!inHScene || collisionHelpers == null)
                    return;

                foreach (var helper in collisionHelpers.Where(helper => helper != null))
                {
                    if (!optimizeCollisionHelpers.Value)
                        helper.forceUpdate = true;
                    
                    helper.updateOncePerFrame = optimizeCollisionHelpers.Value;
                }
            };
            
            var harmony = new Harmony("AI_BetterHScenes_1");
            
            //-- Strip male/futa pants when starting H --//
            var type_1 = typeof(HScene).GetNestedType("<StartAnim>c__Iterator1", BindingFlags.NonPublic);
            var method_1 = type_1.GetMethod("MoveNext");
            var postfix_1 = new HarmonyMethod(typeof(AI_BetterHScenes), nameof(HScene_StartAnim_StripMalePants));
            harmony.Patch(method_1, null, postfix_1);
            
            //-- Strip male/futa pants when changing animation --//
            var type_2 = typeof(HScene).GetNestedType("<ChangeAnimation>c__Iterator2", BindingFlags.NonPublic);
            var method_2 = type_2.GetMethod("MoveNext");
            var postfix_2 = new HarmonyMethod(typeof(AI_BetterHScenes), nameof(HScene_ChangeAnimation_StripMalePants));
            harmony.Patch(method_2, null, postfix_2);
            
            HarmonyWrapper.PatchAll(typeof(Transpilers));
            HarmonyWrapper.PatchAll(typeof(AI_BetterHScenes));
        }

        //-- Always gauges heart --//
        [HarmonyPostfix, HarmonyPatch(typeof(FeelHit), "isHit")]
        public static void FeelHit_isHit_AlwaysGaugesHeart(ref bool __result)
        {
            if(inHScene && alwaysGaugesHeart.Value)
                __result = true;
        }
        
        //-- Toggle chara position draggers --//
        private void Update()
        {
            if (!positionDraggers.Value || !inHScene || draggers == null || draggers.Count == 0)
                return;

            foreach (var dragger in draggers.Where(dragger => dragger != null && dragger.gameObject != null))
            {
                ChaControl chara = dragger.transform.parent.gameObject.GetComponent<ChaControl>();
                dragger.gameObject.SetActiveIfDifferent(UnityEngine.Input.GetKey(showMaleDraggers.Value.MainKey) && chara.sex == 0 || UnityEngine.Input.GetKey(showFemaleDraggers.Value.MainKey) && chara.sex == 1);
            }
        }

        //-- Disable camera control when chara position dragging --//
        [HarmonyPrefix, HarmonyPatch(typeof(VirtualCameraController), "LateUpdate")]
        public static bool VirtualCameraController_LateUpdate_Patch(VirtualCameraController __instance)
        {
            if (!cameraShouldLock || !inHScene || !positionDraggers.Value || draggers == null || draggers.Count == 0 || hCamera == null)
                return true;

            if (!draggers.Any(comp => comp != null && comp.isClicked)) 
                return true;
            
            Traverse.Create(__instance).Property("isControlNow").SetValue(false);
            return false;
        }

        //-- Make girl cry if weakness is reached --//
        //-- Close girl eyes if weakness is reached --//
        [HarmonyPrefix, HarmonyPatch(typeof(HVoiceCtrl), "SetFace")]
        public static void HVoiceCtrl_SetFace_ForceTearsOnWeakness(HVoiceCtrl __instance, ref HVoiceCtrl.FaceInfo _face)
        {
            if (!inHScene || !__instance.ctrlFlag.isFaintness || _face == null)
                return;

            if(forceTearsOnWeakness.Value) 
                _face.tear = 1f;

            if (!forceCloseEyesOnWeakness.Value) 
                return;
            
            _face.openEye = 0.05f;
            _face.blink = false;
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
        //-- Create chara draggers --//
        [HarmonyPostfix, HarmonyPatch(typeof(HScene), "SetStartVoice")]
        public static void HScene_SetStartVoice_Patch(HScene __instance)
        {
            inHScene = true;
            
            Tools.SetupVariables(__instance);
            
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

            if (hCamera != null && unlockCamera.Value)
            {
                hCamera.isLimitDir = false;
                hCamera.isLimitPos = false;
            }

            if (hPointSearchRange.Value != 60 && hSprite != null)
                hSprite.HpointSearchRange = hPointSearchRange.Value;

            Tools.CreateDraggers();
        }

        //-- Enable map after H if disabled previously --//
        //-- Enable map simulation after H if disabled previously --//
        //-- Destroy chara draggers --//
        [HarmonyPrefix, HarmonyPatch(typeof(HScene), "EndProc")]
        public static void HScene_EndProc_Patch()
        {
            inHScene = false;

            if (map != null && mapShouldEnable)
            {
                map.SetActive(true);
                mapShouldEnable = false;
            }
            
            if (mapSimulation != null && mapSimulationShouldEnable)
            {
                mapSimulation.SetActive(true);
                mapSimulationShouldEnable = false;
            }

            Tools.DestroyDraggers();
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
        
        //-- Strip male/futa pants when starting H --//
        //-- Strip male/futa pants when changing animation --//
        private static void HScene_StartAnim_StripMalePants() => HScene_StripMalePants(stripMalePantsStartH.Value);
        private static void HScene_ChangeAnimation_StripMalePants() => HScene_StripMalePants(stripMalePantsChangeAnim.Value);
        private static void HScene_StripMalePants(bool shouldStrip)
        {
            if (!shouldStrip || manager == null || manager.Player == null)
                return;

            ChaControl ply = manager.Player.ChaControl;
            if ((ply.sex == 0 || manager.bFutanari) && ply.IsClothesStateKind(1))
                ply.SetClothesState(1, 2);
        }

        //-- Add character to the shouldCleanUp list & add bath desire --//
        [HarmonyPostfix, HarmonyPatch(typeof(SiruPasteCtrl), "Proc")]
        public static void SiruPasteCtrl_Proc_PopulateList(SiruPasteCtrl __instance)
        {
            if (!inHScene || !retainCumAfterH.Value || shouldCleanUp == null)
                return;

            ChaControl chara = Traverse.Create(__instance).Field("chaFemale").GetValue<ChaControl>();
            if (chara == null || shouldCleanUp.Contains(chara))
                return;

            if (manager != null && (manager.bMerchant || manager.Player.ChaControl == chara))
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

                float clampedReason = Tools.Remap(agent.GetFlavorSkill(FlavorSkill.Type.Reason), 0, 99999f, 0, 100f);
                float clampedDirty = Tools.Remap(agent.GetFlavorSkill(FlavorSkill.Type.Dirty), 0, 99999f, 0, 100f);
                float clampedLewd = agent.GetDesire(lewdDesireType) ?? 0;
                float newBathDesire = 100f + clampedReason - clampedDirty - clampedLewd * 1.25f;
                
                agent.SetDesire(bathDesireType, Mathf.Clamp(newBathDesire, 0f, 100f));
                break;
            }
        }
        
        //-- Clean up chara after bath if retaining cum effect --//
        [HarmonyPostfix, HarmonyPatch(typeof(Bath), "OnCompletedStateTask")]
        public static void Bath_OnCompletedStateTask_CleanUp(Bath __instance) => Tools.CleanUpSiru(__instance);
        
        //-- Clean up chara after changing if retaining cum effect --//
        [HarmonyPostfix, HarmonyPatch(typeof(ClothChange), "OnCompletedStateTask")]
        public static void ClothChange_OnCompletedStateTask_CleanUp(ClothChange __instance) => Tools.CleanUpSiru(__instance);
    }
}