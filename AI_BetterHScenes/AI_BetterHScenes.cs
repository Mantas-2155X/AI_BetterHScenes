using System.Collections.Generic;
using HarmonyLib;

using BepInEx;
using BepInEx.Harmony;
using BepInEx.Configuration;

using UnityEngine;

namespace AI_BetterHScenes
{
    [BepInPlugin(nameof(AI_BetterHScenes), nameof(AI_BetterHScenes), VERSION)][BepInProcess("AI-Syoujyo")]
    public class AI_BetterHScenes : BaseUnityPlugin
    {
        public const string VERSION = "1.0.1";

        private static bool inHScene;
            
        private static GameObject map;
        private static List<SkinnedCollisionHelper> collisionHelpers;
        
        private static bool mapShouldEnable; // compatibility with other plugins which might disable the map
        
        private static ConfigEntry<bool> disableMap { get; set; }
        private static ConfigEntry<bool> optimizeCollisionHelpers { get; set; }
        
        private void Awake()
        {
            disableMap = Config.Bind("Performance Improvements", "Disable map", false, new ConfigDescription("Disable map during H scene"));
            optimizeCollisionHelpers = Config.Bind("Performance Improvements", "Optimize collisionhelpers", true, new ConfigDescription("Optimize collisionhelpers by letting them update once per frame"));

            disableMap.SettingChanged += delegate
            {
                if (map == null || !inHScene)
                    return;
                
                map.SetActive(!disableMap.Value);
                mapShouldEnable = disableMap.Value;
            };
            
            optimizeCollisionHelpers.SettingChanged += delegate
            {
                if (collisionHelpers == null || !inHScene)
                    return;

                foreach (var helper in collisionHelpers)
                    helper.updateOncePerFrame = optimizeCollisionHelpers.Value;
            };
            
            HarmonyWrapper.PatchAll(typeof(AI_BetterHScenes));
        }
        
        //-- Disable map during H to improve performance --//
        [HarmonyPostfix, HarmonyPatch(typeof(HScene), "SetStartVoice")]
        public static void HScene_SetStartVoice_DisableMap()
        {
            inHScene = true;
            
            map = GameObject.Find("map00_Beach");
            collisionHelpers = new List<SkinnedCollisionHelper>();

            if (map == null || !disableMap.Value) 
                return;
            
            map.SetActive(false);
            mapShouldEnable = true;
        }
        
        //-- Enable map after H if disabled previously --//
        [HarmonyPrefix, HarmonyPatch(typeof(HScene), "EndProc")]
        public static void HScene_EndProc_EnableMap()
        {
            inHScene = false;
            
            if (map == null) 
                return;

            if (!disableMap.Value && !mapShouldEnable) 
                return;
            
            map.SetActive(true);
            mapShouldEnable = false;
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
