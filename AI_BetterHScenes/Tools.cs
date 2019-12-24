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
        public static class DraggerData
        {
            public static readonly string[] names =
            {
                "XYZ",
                "X",
                "Y",
                "Z"
            };
        
            public static readonly Color[] colors =
            {
                new Color(0.5f, 0.5f, 0.5f, 0.75f), 
                new Color(0.5f, 0, 0, 0.75f), 
                new Color(0, 0.5f, 0, 0.75f), 
                new Color(0, 0, 0.5f, 0.75f)
            };

            public static readonly Vector3[] scales =
            {
                new Vector3(0.5f, 0.5f, 0.5f),
                new Vector3(3, 0.5f, 0.5f),
                new Vector3(0.5f, 3, 0.5f),
                new Vector3(0.5f, 0.5f, 3)
            };
        
            public static readonly Vector3[] positions =
            {
                new Vector3(0, 0, 0),
                new Vector3(1.5f, 0, 0),
                new Vector3(0, 1.5f, 0),
                new Vector3(0, 0, 1.5f)
            };
        }
        
        public static float Remap(float value, float from1, float to1, float from2, float to2) 
        {
            return (value - from1) / (to1 - from1) * (to2 - from2) + from2;
        }
        
        public static int ChangeSiruIndex()
        {
            if (AI_BetterHScenes.manager != null && AI_BetterHScenes.manager.bMerchant && AI_BetterHScenes.cleanMerchantCumAfterH.Value)
                return 0;
            
            return AI_BetterHScenes.retainCumAfterH.Value ? 5 : 0;
        }
        
        public static bool ChangeUIEnableIndex()
        {
            return AI_BetterHScenes.keepButtonsInteractive.Value;
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
        
        public static void CreateDraggers()
        {
            foreach (var chara in AI_BetterHScenes.characters)
            {
                if (chara == null)
                    continue;
                
                var cObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cObj.transform.SetParent(chara.transform);
                cObj.layer = 10;
                cObj.name = "XYZ";
                cObj.transform.localPosition = Vector3.zero;
                cObj.transform.eulerAngles = Vector3.zero;

                var centerCopy = Object.Instantiate(cObj);
                if (centerCopy == null) 
                    return;
                
                for (int i = 0; i < 3; i++)
                    Object.Instantiate(centerCopy, cObj.transform);
                
                Object.Destroy(centerCopy);
                
                cObj.AddComponent<DraggerComponent>().SetData(AI_BetterHScenes.hCamera);
                cObj.SetActive(false);
                
                AI_BetterHScenes.draggers.Add(cObj.GetComponent<DraggerComponent>());
            }
        }
        
        public static void DestroyDraggers()
        {
            if (AI_BetterHScenes.characters == null || AI_BetterHScenes.characters.Count == 0)
                return;

            foreach (var dragger in AI_BetterHScenes.draggers)
            {
                if (dragger == null || dragger.gameObject == null)
                    continue;
                
                Object.Destroy(dragger.gameObject);
            }
        }

        public static void SetupVariables(HScene __instance)
        {
            AI_BetterHScenes.map = GameObject.Find("map00_Beach");
            if (AI_BetterHScenes.map == null)
                AI_BetterHScenes.map = GameObject.Find("map_01_data");

            AI_BetterHScenes.mapSimulation = GameObject.Find("CommonSpace/MapRoot/MapSimulation(Clone)");
            AI_BetterHScenes.collisionHelpers = new List<SkinnedCollisionHelper>();
            
            AI_BetterHScenes.hScene = __instance;
            AI_BetterHScenes.hFlagCtrl = __instance.ctrlFlag;
            AI_BetterHScenes.manager = Traverse.Create(__instance).Field("hSceneManager").GetValue<HSceneManager>();
            AI_BetterHScenes.hSprite = Traverse.Create(__instance).Field("sprite").GetValue<HSceneSprite>();
            
            if (__instance.ctrlFlag != null && __instance.ctrlFlag.cameraCtrl != null)
                AI_BetterHScenes.hCamera = __instance.ctrlFlag.cameraCtrl;
            
            AI_BetterHScenes.characters = new List<ChaControl>();
            AI_BetterHScenes.characters.AddRange(__instance.GetFemales());
            AI_BetterHScenes.characters.AddRange(__instance.GetMales());
            
            AI_BetterHScenes.draggers = new List<DraggerComponent>();
            AI_BetterHScenes.cameraShouldLock = true;
        }
    }
}