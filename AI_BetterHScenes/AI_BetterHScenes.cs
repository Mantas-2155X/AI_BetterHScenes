using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using HarmonyLib;

using BepInEx;
using BepInEx.Harmony;
using BepInEx.Configuration;

using AIProject;
using Manager;
using UnityEngine;

namespace AI_BetterHScenes
{
    [BepInPlugin(nameof(AI_BetterHScenes), nameof(AI_BetterHScenes), VERSION)][BepInProcess("AI-Syoujyo")]
    public class AI_BetterHScenes : BaseUnityPlugin
    {
        public const string VERSION = "1.1.0";

        private static bool inHScene;

        private static VirtualCameraController hCamera;
        
        private static GameObject map;
        private static GameObject mapSimulation;
        private static List<SkinnedCollisionHelper> collisionHelpers;
        
        private static bool mapShouldEnable; // compatibility with other plugins which might disable the map
        private static bool mapSimulationShouldEnable; // compatibility with other plugins which might disable the map
        
        private static ConfigEntry<bool> stripMalePantsStartH { get; set; }
        private static ConfigEntry<bool> stripMalePantsChangeAnim { get; set; }
        private static ConfigEntry<bool> unlockCamera { get; set; }
        
        private static ConfigEntry<bool> disableMap { get; set; }
        private static ConfigEntry<bool> disableMapSimulation { get; set; }
        private static ConfigEntry<bool> optimizeCollisionHelpers { get; set; }
        
        private void Awake()
        {
            stripMalePantsStartH = Config.Bind("QoL", "Strip male pants on H start", true, new ConfigDescription("Strip male/futa pants when starting H"));
            stripMalePantsChangeAnim = Config.Bind("QoL", "Strip male pants on anim change & start", false, new ConfigDescription("Strip male/futa pants when changing H animation & starting H"));
            unlockCamera = Config.Bind("QoL", "Unlock camera movement", true, new ConfigDescription("Unlock camera zoom out / distance limit during H"));
            
            disableMap = Config.Bind("Performance Improvements", "Disable map", false, new ConfigDescription("Disable map during H scene"));
            disableMapSimulation = Config.Bind("Performance Improvements", "Disable map simulation", false, new ConfigDescription("Disable map simulation during H scene (WARNING: May cause some effects to disappear)"));
            optimizeCollisionHelpers = Config.Bind("Performance Improvements", "Optimize collisionhelpers", true, new ConfigDescription("Optimize collisionhelpers by letting them update once per frame"));

            unlockCamera.SettingChanged += delegate
            {
                if (hCamera == null || !inHScene)
                    return;
                
                hCamera.isLimitDir = !unlockCamera.Value;
                hCamera.isLimitPos = !unlockCamera.Value;
            };

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
            
            HarmonyWrapper.PatchAll(typeof(AI_BetterHScenes));
        }

        public static void HScene_StartAnim_StripMalePants() => HScene_StripMalePants(stripMalePantsStartH.Value);
        public static void HScene_ChangeAnimation_StripMalePants() => HScene_StripMalePants(stripMalePantsChangeAnim.Value);
        
        //-- Strip male/futa pants when starting H --//
        //-- Strip male/futa pants when changing animation --//
        public static void HScene_StripMalePants(bool shouldStrip)
        {
            if (!shouldStrip || !Singleton<HSceneManager>.IsInstance())
                return;

            HSceneManager manager = Singleton<HSceneManager>.Instance;
            if (manager == null || manager.Player == null)
                return;

            AIChara.ChaControl ply = manager.Player.ChaControl;
            if ((ply.sex == 0 || manager.bFutanari) && ply.IsClothesStateKind(1))
                ply.SetClothesState(1, 2);
        }
       
        //-- Disable map during H to improve performance --//
        //-- Disable map simulation during H to improve performance --//
        //-- Remove hcamera movement limit --//
        [HarmonyPostfix, HarmonyPatch(typeof(HScene), "SetStartVoice")]
        public static void HScene_SetStartVoice_DisableMap_UnlockCamera(HScene __instance)
        {
            inHScene = true;
            
            map = GameObject.Find("map00_Beach");
            mapSimulation = GameObject.Find("CommonSpace/MapRoot/MapSimulation(Clone)");
            collisionHelpers = new List<SkinnedCollisionHelper>();
            
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
                if (disableMap.Value || mapShouldEnable)
                {
                    map.SetActive(true);
                    mapShouldEnable = false;
                }
            
            if(mapSimulation != null)
                if (disableMapSimulation.Value || mapSimulationShouldEnable)
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
