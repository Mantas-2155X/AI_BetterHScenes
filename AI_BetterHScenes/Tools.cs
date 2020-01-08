using System.Collections.Generic;

using HarmonyLib;

using AIChara;
using AIProject;
using Manager;
using UnityEngine;

namespace AI_BetterHScenes
{
    public static class Tools
    {
        public enum CleanCum
        {
            Off,
            MerchantOnly,
            AgentsOnly,
            All
        }
        
        public enum OffHStartAnimChange
        {
            Off,
            OnHStart,
            OnHStartAndAnimChange
        }
        
        public enum OffWeaknessAlways
        {
            Off,
            WeaknessOnly,
            Always
        }

        public enum ClothesStrip
        {
            Off,
            Half,
            All
        }

        public enum AutoFinish
        {
            Off,
            ServiceOnly,
            InsertOnly,
            Both
        }

        public enum AutoServicePrefer
        {
            Drink,
            Spit,
            Outside,
            Random
        }

        public enum AutoInsertPrefer
        {
            Inside,
            Outside,
            Same,
            Random
        }

        private static readonly string[] finishFindTransforms =
        {
            "finishDrinkTex",
            "finishVomitTex",
            "finishOutTex",
            "finishInTex",
            "finishSynchroTex"
        };
        
        public static float Remap(float value, float from1, float to1, float from2, float to2) 
        {
            return (value - from1) / (to1 - from1) * (to2 - from2) + from2;
        }
        
        public static bool newChangebuttonactive()
        {
            if (AI_BetterHScenes.keepButtonsInteractive.Value && AI_BetterHScenes.hFlagCtrl.nowOrgasm)
                return true;
            
            return !AI_BetterHScenes.hFlagCtrl.nowOrgasm;
        }

        public static bool newUIDisable()
        {
            return AI_BetterHScenes.keepButtonsInteractive.Value;
        }
        
        public static int ChangeSiruIndex()
        {
            if (AI_BetterHScenes.cleanCumAfterH.Value == CleanCum.Off)
                return 5;
            
            if (AI_BetterHScenes.cleanCumAfterH.Value == CleanCum.All)
                return 0;
            
            if (AI_BetterHScenes.cleanCumAfterH.Value == CleanCum.MerchantOnly && AI_BetterHScenes.manager != null && AI_BetterHScenes.manager.bMerchant)
                return 0;

            return 0;
        }
        
        public static void CleanUpSiru(AgentStateAction __instance)
        {
            if (AI_BetterHScenes.shouldCleanUp == null || AI_BetterHScenes.shouldCleanUp.Count == 0 || __instance == null || __instance.Owner == null)
                return;
            
            var tree = (__instance.Owner as AgentBehaviorTree);
            if (tree == null)
                return;

            AgentActor agent = tree.SourceAgent;
            if (agent == null || agent.ChaControl == null || !AI_BetterHScenes.shouldCleanUp.Contains(agent.ChaControl))
                return;

            for (int i = 0; i < 5; i++)
            {
                ChaFileDefine.SiruParts parts = (ChaFileDefine.SiruParts)i;
                agent.ChaControl.SetSiruFlag(parts, 0);
            }

            AI_BetterHScenes.shouldCleanUp.Remove(agent.ChaControl);
        }

        public static void SetupVariables(HScene __instance)
        {
            AI_BetterHScenes.map = GameObject.Find("map00_Beach");
            if (AI_BetterHScenes.map == null)
                AI_BetterHScenes.map = GameObject.Find("map_01_data");

            AI_BetterHScenes.mapSimulation = GameObject.Find("CommonSpace/MapRoot/MapSimulation(Clone)");
            AI_BetterHScenes.collisionHelpers = new List<SkinnedCollisionHelper>();
            
            AI_BetterHScenes.hScene = __instance;
            var hTrav = Traverse.Create(__instance);
            
            AI_BetterHScenes.hFlagCtrl = __instance.ctrlFlag;
            AI_BetterHScenes.manager = hTrav.Field("hSceneManager").GetValue<HSceneManager>();
            AI_BetterHScenes.hSprite = hTrav.Field("sprite").GetValue<HSceneSprite>();
            
            AI_BetterHScenes.characters = new List<ChaControl>();
            AI_BetterHScenes.characters.AddRange(__instance.GetMales());
            AI_BetterHScenes.characters.AddRange(__instance.GetFemales());

            AI_BetterHScenes.cameraShouldLock = true;

            if (__instance.ctrlFlag != null)
            {
                Traverse.Create(__instance.ctrlFlag).Field("gotoFaintnessCount").SetValue(AI_BetterHScenes.countToWeakness.Value);
                
                if (__instance.ctrlFlag.cameraCtrl != null)
                    AI_BetterHScenes.hCamera = __instance.ctrlFlag.cameraCtrl;
            }

            AI_BetterHScenes.finishObjects = new List<GameObject>();
            foreach (var name in finishFindTransforms)
                AI_BetterHScenes.finishObjects.Add(AI_BetterHScenes.hSprite.categoryFinish.transform.Find(name).gameObject);

            UI.InitDraggersUI();
        }
    }
}