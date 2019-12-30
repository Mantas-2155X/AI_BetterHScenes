﻿using System.Collections.Generic;
using System.Linq;

using HarmonyLib;

using BepInEx;
using BepInEx.Logging;
using BepInEx.Harmony;
using BepInEx.Configuration;

using AIProject;
using AIProject.Definitions;

using AIChara;
using Manager;

using UnityEngine;

namespace AI_BetterHScenes
{
    [BepInPlugin(nameof(AI_BetterHScenes), nameof(AI_BetterHScenes), VERSION)][BepInProcess("AI-Syoujyo")]
    public class AI_BetterHScenes : BaseUnityPlugin
    {
        public const string VERSION = "2.2.0";

        public new static ManualLogSource Logger;

        private static bool inHScene;

        public static HScene hScene;
        public static HSceneManager manager;
        public static HSceneFlagCtrl hFlagCtrl;
        public static HSceneSprite hSprite;

        public static Camera mainCamera;
        public static VirtualCameraController hCamera;
        
        public static List<ChaControl> characters;
        public static List<ChaControl> shouldCleanUp;

        public static GameObject map;
        public static GameObject mapSimulation;
        public static List<SkinnedCollisionHelper> collisionHelpers;

        private static bool activeUI;
        public static bool cameraShouldLock; // compatibility with other plugins which might disable the camera control
        private static bool mapShouldEnable; // compatibility with other plugins which might disable the map
        private static bool mapSimulationShouldEnable; // compatibility with other plugins which might disable the map simulation
        
        //-- Draggers --//
        private static ConfigEntry<KeyboardShortcut> showDraggerUI { get; set; }
        
        //-- Clothes --//
        private static ConfigEntry<Tools.StripMalePants> stripMalePants { get; set; }
        
        //-- Weakness --//
        public static ConfigEntry<int> countToWeakness { get; private set; }
        private static ConfigEntry<Tools.OffWeaknessAlways> forceTears { get; set; }
        private static ConfigEntry<Tools.OffWeaknessAlways> forceCloseEyes { get; set; }
        private static ConfigEntry<Tools.OffWeaknessAlways> forceStopBlinking { get; set; }
        
        //-- Cum --//
        private static ConfigEntry<bool> autoFinish { get; set; }
        public static ConfigEntry<Tools.CleanCum> cleanCumAfterH { get; private set; }
        private static ConfigEntry<bool> increaseBathDesire { get; set; }
        
        //-- General --//
        private static ConfigEntry<Tools.OffWeaknessAlways> alwaysGaugesHeart { get; set; }
        public static ConfigEntry<bool> keepButtonsInteractive { get; private set; }
        private static ConfigEntry<int> hPointSearchRange { get; set; }
        private static ConfigEntry<bool> unlockCamera { get; set; }
        
        //-- Performance --//
        private static ConfigEntry<bool> disableMap { get; set; }
        private static ConfigEntry<bool> disableMapSimulation { get; set; }
        private static ConfigEntry<bool> optimizeCollisionHelpers { get; set; }
        
        private void Awake()
        {
            Logger = base.Logger;

            shouldCleanUp = new List<ChaControl>();

            showDraggerUI = Config.Bind("QoL > Draggers", "Show draggers UI", new KeyboardShortcut(KeyCode.M));
            
            stripMalePants = Config.Bind("QoL > Clothes", "Strip male pants", Tools.StripMalePants.OnHStart, new ConfigDescription("Strip male pants during H"));
            
            countToWeakness = Config.Bind("QoL > Weakness", "Orgasm count until weakness", 3, new ConfigDescription("How many times does the girl have to orgasm to reach weakness", new AcceptableValueRange<int>(1, 999)));
            forceTears = Config.Bind("QoL > Weakness", "Tears when weakness is reached", Tools.OffWeaknessAlways.WeaknessOnly, new ConfigDescription("Make girl cry when weakness is reached during H"));
            forceCloseEyes = Config.Bind("QoL > Weakness", "Close eyes when weakness is reached", Tools.OffWeaknessAlways.Off, new ConfigDescription("Close girl eyes when weakness is reached during H"));
            forceStopBlinking = Config.Bind("QoL > Weakness", "Stop blinking when weakness is reached", Tools.OffWeaknessAlways.Off, new ConfigDescription("Stop blinking when weakness is reached during H"));

            autoFinish = Config.Bind("QoL > Cum", "Auto finish", false, new ConfigDescription("Automatically finish inside when both gauges reach max"));
            cleanCumAfterH = Config.Bind("QoL > Cum", "Clean cum on body after H", Tools.CleanCum.All, new ConfigDescription("Clean cum on body after H"));
            increaseBathDesire = Config.Bind("QoL > Cum", "Increase bath desire after H", false, new ConfigDescription("Increase bath desire after H (agents only)"));

            alwaysGaugesHeart = Config.Bind("QoL > General", "Always hit gauge heart", Tools.OffWeaknessAlways.WeaknessOnly, new ConfigDescription("Always hit gauge heart. Will cause progress to increase without having to scroll specific amount"));
            keepButtonsInteractive = Config.Bind("QoL > General", "Keep UI buttons interactive*", false, new ConfigDescription("Keep buttons interactive during certain events like orgasm (WARNING: May cause bugs)"));
            hPointSearchRange = Config.Bind("QoL > General", "H point search range", 300, new ConfigDescription("Range in which H points are shown when changing location (default 60)", new AcceptableValueRange<int>(1, 999)));
            unlockCamera = Config.Bind("QoL > General", "Unlock camera movement", true, new ConfigDescription("Unlock camera zoom out / distance limit during H"));
            
            disableMap = Config.Bind("Performance Improvements", "Disable map", false, new ConfigDescription("Disable map during H scene"));
            disableMapSimulation = Config.Bind("Performance Improvements", "Disable map simulation*", false, new ConfigDescription("Disable map simulation during H scene (WARNING: May cause some effects to disappear)"));
            optimizeCollisionHelpers = Config.Bind("Performance Improvements", "Optimize collisionhelpers", true, new ConfigDescription("Optimize collisionhelpers by letting them update once per frame"));

            countToWeakness.SettingChanged += delegate
            {
                if (!inHScene || hFlagCtrl == null)
                    return;
                
                Traverse.Create(hFlagCtrl).Field("gotoFaintnessCount").SetValue(countToWeakness.Value);
            };
            
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
            
            HarmonyWrapper.PatchAll(typeof(Transpilers));
            HarmonyWrapper.PatchAll(typeof(AI_BetterHScenes));
        }

        //-- Draw chara draggers UI --//
        private void OnGUI()
        {
            if(activeUI)
                UI.DrawDraggersUI();
        }

        //-- Auto finish --//
        private void Update()
        {
            if (!inHScene)
                return;

            if (autoFinish.Value && hFlagCtrl != null && hFlagCtrl.feel_f >= 0.96f && hFlagCtrl.feel_m >= 0.96f)
                hFlagCtrl.click = HSceneFlagCtrl.ClickKind.FinishSame;
            
            if (showDraggerUI.Value.IsDown())
                activeUI = !activeUI;
        }
        
        //-- Disable map, simulation to improve performance --//
        //-- Remove hcamera movement limit --//
        //-- Change H point search range --//
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
        }

        //-- Enable map, simulation after H if disabled previously --//
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
        }
        
        //-- Always gauges heart --//
        [HarmonyPostfix, HarmonyPatch(typeof(FeelHit), "isHit")]
        public static void FeelHit_isHit_AlwaysGaugesHeart(ref bool __result)
        {
            if(inHScene && alwaysGaugesHeart.Value == Tools.OffWeaknessAlways.Always || alwaysGaugesHeart.Value == Tools.OffWeaknessAlways.WeaknessOnly && hFlagCtrl != null && hFlagCtrl.isFaintness)
                __result = true;
        }

        //-- Disable camera control when dragger ui open --//
        [HarmonyPrefix, HarmonyPatch(typeof(VirtualCameraController), "LateUpdate")]
        public static bool VirtualCameraController_LateUpdate_Patch(VirtualCameraController __instance)
        {
            if (!cameraShouldLock || !activeUI || !inHScene || __instance == null)
                return true;
            
            Traverse.Create(__instance).Property("isControlNow").SetValue(false);
            return false;
        }
        
        //-- Tears, close eyes, stop blinking --//
        [HarmonyPrefix, HarmonyPatch(typeof(HVoiceCtrl), "SetFace")]
        public static void HVoiceCtrl_SetFace_ForceTearsOnWeakness(HVoiceCtrl __instance, ref HVoiceCtrl.FaceInfo _face)
        {
            if (!inHScene || _face == null)
                return;

            if(forceTears.Value == Tools.OffWeaknessAlways.Always || forceTears.Value == Tools.OffWeaknessAlways.WeaknessOnly && __instance.ctrlFlag.isFaintness) 
                _face.tear = 1f;

            if(forceCloseEyes.Value == Tools.OffWeaknessAlways.Always || forceCloseEyes.Value == Tools.OffWeaknessAlways.WeaknessOnly && __instance.ctrlFlag.isFaintness)
                _face.openEye = 0.05f;
            
            if(forceStopBlinking.Value == Tools.OffWeaknessAlways.Always || forceStopBlinking.Value == Tools.OffWeaknessAlways.WeaknessOnly && __instance.ctrlFlag.isFaintness)
                _face.blink = false;
        }
        
        //-- Keep buttons interactive during certain events like orgasm --//
        [HarmonyPrefix, HarmonyPatch(typeof(HSceneSpriteCategories), "Changebuttonactive")]
        public static void HSceneSpriteCategories_Changebuttonactive_KeepButtonsInteractive(ref bool val)
        {
            if (keepButtonsInteractive.Value && !val)
                val = true;
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
        
        //-- Add character to the shouldCleanUp list & add bath desire --//
        [HarmonyPostfix, HarmonyPatch(typeof(SiruPasteCtrl), "Proc")]
        public static void SiruPasteCtrl_Proc_PopulateList(SiruPasteCtrl __instance)
        {
            if (!inHScene || cleanCumAfterH.Value == Tools.CleanCum.Off || cleanCumAfterH.Value == Tools.CleanCum.MerchantOnly || shouldCleanUp == null)
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

                if (increaseBathDesire.Value)
                {
                    int bathDesireType = Desire.GetDesireKey(Desire.Type.Bath);
                    int lewdDesireType = Desire.GetDesireKey(Desire.Type.H);

                    float clampedReason = Tools.Remap(agent.GetFlavorSkill(FlavorSkill.Type.Reason), 0, 99999f, 0, 100f);
                    float clampedDirty = Tools.Remap(agent.GetFlavorSkill(FlavorSkill.Type.Dirty), 0, 99999f, 0, 100f);
                    float clampedLewd = agent.GetDesire(lewdDesireType) ?? 0;
                    float newBathDesire = 100f + clampedReason - clampedDirty - clampedLewd * 1.25f;

                    agent.SetDesire(bathDesireType, Mathf.Clamp(newBathDesire, 0f, 100f));
                }

                break;
            }
        }
        
        //-- Clean up chara after bath if retaining cum effect --//
        [HarmonyPostfix, HarmonyPatch(typeof(Bath), "OnCompletedStateTask")]
        public static void Bath_OnCompletedStateTask_CleanUp(Bath __instance) => Tools.CleanUpSiru(__instance);
        
        //-- Clean up chara after changing if retaining cum effect --//
        [HarmonyPostfix, HarmonyPatch(typeof(ClothChange), "OnCompletedStateTask")]
        public static void ClothChange_OnCompletedStateTask_CleanUp(ClothChange __instance) => Tools.CleanUpSiru(__instance);
        
        //-- Strip male pants when starting H --//
        [HarmonyPrefix, HarmonyPatch(typeof(HScene), "StartAnim")]
        private static void HScene_StartAnim_StripMalePants() => HScene_StripMalePants(stripMalePants.Value == Tools.StripMalePants.OnHStart);
        
        //-- Strip male pants when changing animation --//
        [HarmonyPrefix, HarmonyPatch(typeof(HScene), "ChangeAnimation")]
        private static void HScene_ChangeAnimation_StripMalePants() => HScene_StripMalePants(stripMalePants.Value == Tools.StripMalePants.OnHStartAndAnimChange);

        private static void HScene_StripMalePants(bool shouldStrip)
        {
            if (!shouldStrip || manager == null || manager.Player == null)
                return;

            ChaControl ply = manager.Player.ChaControl;
            if ((ply.sex == 0 || manager.bFutanari) && ply.IsClothesStateKind(1))
                ply.SetClothesState(1, 2);
        }
    }
}