using System.Collections;
using UnityEngine;
using AIProject;

namespace AI_BetterHScenes
{
    public class DraggerComponent : MonoBehaviour
    {
        private Camera mainCamera;
        private VirtualCameraController ctrl;
        
        private int axis;
        private Material mat;
        private BoxCollider selfCollider;
        
        private Vector3 offset;
        private Vector3 screenPoint;

        private Transform center;
        private Transform character;

        private string objName;
        private Vector3 lScale;
        private Vector3 lPosition;
        
        private Color idleColor;
        private Color selectedColor;

        public bool isHover;
        public bool isClicked;

        private Vector3 lockAngle;
        
        private readonly RaycastHit[] hits = new RaycastHit[15];
        
        public void SetData(Transform _character, Transform _center, int _axis, VirtualCameraController _hCamera)
        {
            character = _character;
            center = _center;
            axis = _axis;

            selfCollider = gameObject.GetComponent<BoxCollider>();
            mainCamera = Camera.main;
            ctrl = _hCamera;

            switch (axis)
            {
                case 0:
                    objName = "X";
                    lScale = new Vector3(3, 0.5f, 0.5f);
                    lPosition = new Vector3(1.5f, 0, 0);
                    idleColor = new Color(0.75f, 0, 0, 0.75f);
                    break;
                case 1:
                    objName = "Y";
                    lScale = new Vector3(0.5f, 3, 0.5f);
                    lPosition = new Vector3(0, 1.5f, 0);
                    idleColor = new Color(0, 0.75f, 0, 0.75f);
                    break;
                case 2:
                    objName = "Z";
                    lScale = new Vector3(0.5f, 0.5f, 3);
                    lPosition = new Vector3(0, 0, 1.5f);
                    idleColor = new Color(0, 0, 0.75f, 0.75f);
                    break;
            }

            gameObject.name = objName;
            
            transform.localScale = lScale;
            transform.localPosition = lPosition;
            transform.eulerAngles = Vector3.zero;
            
            selectedColor = new Color(idleColor.r * 1.25f, idleColor.g * 1.25f, idleColor.b * 1.25f, 0.75f);

            mat = gameObject.GetComponent<Renderer>().material;
            mat.color = idleColor;
        }

        private IEnumerator LockCamera()
        {
            ctrl.CameraAngle = lockAngle;
            ctrl.NoCtrlCondition = () => isClicked;
            yield return new WaitUntil(() => !isClicked);
            ctrl.NoCtrlCondition = () => !isClicked;
        }

        private void Update()
        {
            if (ctrl == null || !transform.parent.gameObject.activeSelf)
            {
                if (isClicked)
                    isClicked = false;
                
                return;
            }
            
            if(isClicked)
                StartCoroutine(LockCamera());
        }
        
        private void LateUpdate()
        {
            if (mainCamera == null || ctrl == null || !transform.parent.gameObject.activeSelf)
            {
                if (isClicked)
                    isClicked = false;
                
                return;
            }

            isHover = false;

            if (!isClicked)
            {
                Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
                int hitCount = Physics.RaycastNonAlloc(ray, hits, 250, 1 << 10);

                for (int i = 0; i < hitCount; i++)
                    if (hits[i].collider == selfCollider)
                    {
                        isHover = true;
                        break;
                    }
            }

            if (isClicked && Input.GetKeyUp(KeyCode.Mouse0))
                isClicked = false;

            if (isClicked && !Input.GetKeyDown(KeyCode.Mouse0) && Input.GetKey(KeyCode.Mouse0))
            {
                Vector3 charaPos = character.position;
                Vector3 mousePos = Input.mousePosition;
            
                Vector3 curScreenPoint = new Vector3(mousePos.x, mousePos.y, screenPoint.z);
                Vector3 curPosition = mainCamera.ScreenToWorldPoint(curScreenPoint) + offset;

                character.position = new Vector3(axis == 0 ? curPosition.x : charaPos.x, axis == 1 ? curPosition.y : charaPos.y, axis == 2 ? curPosition.z : charaPos.z);
            }

            if (isHover)
            {
                mat.color = selectedColor;

                if (Input.GetKeyDown(KeyCode.Mouse0))
                {
                    isClicked = true;
                    lockAngle = ctrl.CameraAngle;

                    Vector3 centerPos = center.position;
                    Vector3 mousePos = Input.mousePosition;

                    screenPoint = mainCamera.WorldToScreenPoint(centerPos);
                    offset = centerPos - mainCamera.ScreenToWorldPoint(new Vector3(mousePos.x, mousePos.y, screenPoint.z));
                }
            }
            else
            {
                mat.color = idleColor;
                return;
            }

            if(isClicked)
                StartCoroutine(LockCamera());
        }
    }
}