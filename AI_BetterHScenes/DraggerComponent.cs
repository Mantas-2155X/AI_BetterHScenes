using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using AIProject;

namespace AI_BetterHScenes
{
    public class DraggerComponent : MonoBehaviour
    {
        private Camera mainCamera;
        private VirtualCameraController cameraCtrl;
        
        private bool isSelected;
        private bool isClicked;

        private Transform selectedAxis;
        
        private Vector3 offset;
        private Vector3 lockAngle;
        private Vector3 screenPoint;
        
        private Transform toMove;
        
        private readonly RaycastHit[] hits = new RaycastHit[15];

        private readonly Color[] selectedColors = new Color[4];
        private readonly Material[] materials = new Material[4];
        private readonly Transform[] axisTransforms = new Transform[4];
        private readonly BoxCollider[] boxColliders = new BoxCollider[4];

        public void SetData(VirtualCameraController hCamera)
        {
            mainCamera = Camera.main;
            cameraCtrl = hCamera;
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
        
        private IEnumerator LockCamera()
        {
            cameraCtrl.CameraAngle = lockAngle;
            
            cameraCtrl.NoCtrlCondition = () => isClicked;
            yield return new WaitUntil(() => !isClicked);
            cameraCtrl.NoCtrlCondition = () => !isClicked;
        }
        
        private void Update()
        {
            if (cameraCtrl == null || !gameObject.activeSelf)
            {
                OnDisable();
                return;
            }
            
            if(isClicked)
                StartCoroutine(LockCamera());
        }

        private void LateUpdate()
        {
            if (cameraCtrl == null || !gameObject.activeSelf)
            {
                OnDisable();
                return;
            }

            int selectedIndex = selectedAxis != null ? Array.IndexOf(axisTransforms, selectedAxis) : 0;
            isSelected = false;

            if (!isClicked)
            {
                Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
                int hitCount = Physics.RaycastNonAlloc(ray, hits, 250, 1 << 10);
                
                for (int i = 0; i < hitCount; i++)
                    if (boxColliders.Contains(hits[i].collider))
                    {
                        isSelected = true;
                        selectedAxis = hits[i].collider.transform;
                        
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
                if (Input.GetKeyDown(KeyCode.Mouse0))
                {
                    isClicked = true;
                    lockAngle = cameraCtrl.CameraAngle;

                    Vector3 centerPos = transform.position;
                    Vector3 mousePos = Input.mousePosition;

                    screenPoint = mainCamera.WorldToScreenPoint(centerPos);
                    offset = centerPos - mainCamera.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, screenPoint.z));
                }
            }
            else
            {
                for (int i = 0; i < 4; i++)
                {
                    if (i == selectedIndex && isClicked)
                        continue;
                    
                    materials[i].color = Tools.DraggerData.colors[i];
                }

                return;
            }

            if(isClicked)
                StartCoroutine(LockCamera());
        }
        
        private void OnDisable()
        {
            isClicked = false;
            isSelected = false;
        }
    }
}