using System;
using System.Linq;

using AIProject;

using UnityEngine;

namespace AI_BetterHScenes
{
    public class DraggerComponent : MonoBehaviour
    {
        private Camera mainCamera;
        
        private bool isSelected;
        public bool isClicked;

        private Transform selectedAxis;
        
        private Vector3 offset;
        private Vector3 screenPoint;
        
        private Transform toMove;

        private readonly Color[] selectedColors = new Color[4];
        private readonly Material[] materials = new Material[4];
        private readonly Transform[] axisTransforms = new Transform[4];
        private readonly BoxCollider[] boxColliders = new BoxCollider[4];

        public void SetData(VirtualCameraController hCamera)
        {
            mainCamera = Camera.main;
            toMove = transform.parent;
            
            for (int i = 0; i < 4; i++)
            {
                axisTransforms[i] = i == 0 ? transform : transform.GetChild(i - 1);
                axisTransforms[i].name = Tools.DraggerData.names[i];

                axisTransforms[i].localScale = Tools.DraggerData.scales[i];
                axisTransforms[i].localPosition = Tools.DraggerData.positions[i];
                axisTransforms[i].eulerAngles = Vector3.zero;

                boxColliders[i] = axisTransforms[i].gameObject.GetComponent<BoxCollider>();
                
                materials[i] = axisTransforms[i].gameObject.GetComponent<Renderer>().material;
                materials[i].color = Tools.DraggerData.colors[i];
                
                selectedColors[i] = new Color(materials[i].color.r * 2, materials[i].color.g * 2, materials[i].color.b * 2, 0.75f);
            }
        }

        private void LateUpdate()
        {
            if (!gameObject.activeSelf)
            {
                OnDisable();
                return;
            }
            
            transform.eulerAngles = Vector3.zero;
            
            int selectedIndex = selectedAxis != null ? Array.IndexOf(axisTransforms, selectedAxis) : 0;
            isSelected = false;

            if (!isClicked)
            {
                int hitCount = Physics.RaycastNonAlloc(mainCamera.ScreenPointToRay(Input.mousePosition), AI_BetterHScenes.hits, 250, 1 << 10);
                
                for (int i = 0; i < hitCount; i++)
                    if (boxColliders.Contains(AI_BetterHScenes.hits[i].collider))
                    {
                        isSelected = true;
                        selectedAxis = AI_BetterHScenes.hits[i].collider.transform;
                        
                        break;
                    }
            }

            if (isClicked && Input.GetKeyUp(KeyCode.Mouse0))
                isClicked = false;
            
            if (isClicked && !Input.GetKeyDown(KeyCode.Mouse0) && Input.GetKey(KeyCode.Mouse0))
            {
                Vector3 toMovePos = toMove.position;
                Vector3 mousePos = Input.mousePosition;
            
                Vector3 curScreenPoint = new Vector3(mousePos.x, mousePos.y, screenPoint.z);
                Vector3 curPosition = mainCamera.ScreenToWorldPoint(curScreenPoint) + offset;

                toMove.position = new Vector3(
                    selectedAxis == axisTransforms[0] || selectedAxis == axisTransforms[1] ? curPosition.x : toMovePos.x,
                    selectedAxis == axisTransforms[0] || selectedAxis == axisTransforms[2] ? curPosition.y : toMovePos.y,
                    selectedAxis == axisTransforms[0] || selectedAxis == axisTransforms[3] ? curPosition.z : toMovePos.z
                );
                
                materials[selectedIndex].color = selectedColors[selectedIndex];
            }

            if (isSelected)
            {
                if (!Input.GetKeyDown(KeyCode.Mouse0)) 
                    return;

                if (AI_BetterHScenes.draggers.Any(dragger => dragger != null && dragger.isClicked))
                    return;

                isClicked = true;

                Vector3 centerPos = transform.position;
                Vector3 mousePos = Input.mousePosition;

                screenPoint = mainCamera.WorldToScreenPoint(centerPos);
                offset = centerPos - mainCamera.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, screenPoint.z));
            }
            else
            {
                for (int i = 0; i < 4; i++)
                {
                    if (i == selectedIndex && isClicked)
                        continue;
                    
                    materials[i].color = Tools.DraggerData.colors[i];
                }
            }
        }
        
        private void OnDisable()
        {
            isClicked = false;
            isSelected = false;
        }
    }
}