using System.Collections.Generic;
using UnityEngine;
using AIProject;
using AIChara;
using HarmonyLib;
using Manager;

namespace AI_BetterHScenes
{
    public static class Tools
    {
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
                cObj.name = "DraggerCenter";

                var mat = cObj.GetComponent<Renderer>().material;
                mat.color = new Color(chara.sex, chara.sex, chara.sex, 0.75f);

                var centerTransform = cObj.transform;

                centerTransform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
                centerTransform.localPosition = new Vector3(0f, 0f, 0f);
                centerTransform.eulerAngles = Vector3.zero;

                var centerCopy = Object.Instantiate(cObj);
                if (centerCopy == null) 
                    continue;
                
                for (int i = 0; i < 3; i++)
                {
                    var copy = Object.Instantiate(centerCopy, centerTransform);
                    copy.AddComponent<DraggerComponent>().SetData(chara.transform, centerTransform, i, AI_BetterHScenes.hCamera);
                }
                cObj.SetActive(false);

                Object.Destroy(centerCopy);
            }
        }
        
        public static void DestroyDraggers()
        {
            if (AI_BetterHScenes.characters == null || AI_BetterHScenes.characters.Count == 0)
                return;

            foreach (var chara in AI_BetterHScenes.characters)
            {
                if (chara == null)
                    continue;
                
                var tr = chara.transform;
                if(tr == null)
                    continue;

                Transform dragger = tr.Find("DraggerCenter");
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
        }
    }
}