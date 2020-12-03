using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.ARFoundation;
using UnityEngine.EventSystems;

public class Raycast : MonoBehaviour
{
    Camera m_WorldSpaceCanvasCamera;

    public Camera worldSpaceCanvasCamera
    {
        get { return m_WorldSpaceCanvasCamera; }
        set { m_WorldSpaceCanvasCamera = value; }
    }

    ARRaycastManager arRay;
    Canvas uiRoot;
    public Pose placementPose;
    public GameObject placementIndicator;
    bool placementPoseIsValid;
    public GameObject prefab;
    public GameObject coordinate;

    public Camera arCamera;
    public ARSessionOrigin aRSession;

    public Text debug;

    public float horRef = 0;

    bool placementModePick = false;

    public GameObject cube;

    #region 初始化
    void Awake()
    {
        arRay = FindObjectOfType<ARRaycastManager>();
        uiRoot = FindObjectOfType<Canvas>();
        debug = TransformHelper.FindChild( uiRoot.transform,"Debug").GetComponent<Text>();
        aRSession = FindObjectOfType<ARSessionOrigin>();
        arCamera = aRSession.GetComponentInChildren<Camera>();
    }

    void OnEnable()
    {
        TransformHelper.FindChild(uiRoot.transform, "Mode").GetComponentInChildren<Button>().onClick.AddListener(ChangeMode);
        TransformHelper.FindChild(uiRoot.transform, "Place").GetComponentInChildren<Button>().onClick.AddListener(PlacePrefab);
        TransformHelper.FindChild(uiRoot.transform, "SetRef").GetComponentInChildren<Button>().onClick.AddListener(SetRef);
        TransformHelper.FindChild(uiRoot.transform, "PlaceCoordinate").GetComponentInChildren<Button>().onClick.AddListener(PlaceCoordiante);
        TransformHelper.FindChild(uiRoot.transform, "ClearCoordinate").GetComponentInChildren<Button>().onClick.AddListener(ClearAllCoordinate);
    }

    void OnDisable()
    {
        TransformHelper.FindChild( uiRoot.transform,"Mode").GetComponentInChildren<Button>().onClick.RemoveListener(ChangeMode);
        TransformHelper.FindChild( uiRoot.transform,"Place").GetComponentInChildren<Button>().onClick.RemoveListener(PlacePrefab);
        TransformHelper.FindChild( uiRoot.transform,"SetRef").GetComponentInChildren<Button>().onClick.RemoveListener(SetRef);
        TransformHelper.FindChild(uiRoot.transform, "PlaceCoordinate").GetComponentInChildren<Button>().onClick.RemoveListener(PlaceCoordiante);
        TransformHelper.FindChild(uiRoot.transform, "ClearCoordinate").GetComponentInChildren<Button>().onClick.RemoveListener(ClearAllCoordinate);
    }
    #endregion

    #region 按钮点击
    public void ChangeMode()
    {
        placementModePick = !placementModePick;
    }

    public void PlaceCoordiante()
    {
        placementPose.rotation = Quaternion.LookRotation(Vector3.forward);
        GameObjectPool.instance.CreateObject("Coordinate", coordinate, placementPose.position, placementPose.rotation);
    }

    public void ClearAllCoordinate()
    {
        for(int i=0;i<GameObjectPool.instance.transform.childCount;i++)
        {
            if(GameObjectPool.instance.transform.GetChild(i).gameObject.activeSelf)
            {
                GameObjectPool.instance.CollectGameObejct(GameObjectPool.instance.transform.GetChild(i).gameObject);
            }
        }
    }

    public void PlacePrefab()
    {
        if(!prefab.activeSelf)
        {
            prefab.SetActive(true);
            var cameraForward = Camera.current.transform.forward;
            var cameraBearing = new Vector3(cameraForward.x, 0, cameraForward.z).normalized;
            placementPose.rotation = Quaternion.LookRotation(cameraBearing);

            prefab.transform.rotation = placementPose.rotation;
            prefab.transform.position = placementPose.position;
        }
        else
        {
            prefab.SetActive(false);
        }
    }

    public void SetRef()
    {
        horRef = placementPose.position.y;
    }
    #endregion

    #region 放置标记物

    private void UpdateUI()
    {
        var pos= new Vector3(placementPose.position.x,placementPose.position.y-horRef,placementPose.position.z);
        //placementIndicator.transform.GetComponentInChildren<Text>().text = placementPoseIsValid + "\n" + pos.ToString()+"\n"+horRef;
    }

    private void UpdatePlacementIndicator()
    {
        if(placementPoseIsValid)
        {
            placementIndicator.SetActive(true);

            var cameraForward = Camera.current.transform.forward;
            var cameraBearing = new Vector3(-cameraForward.x, 0, -cameraForward.z).normalized;
            placementPose.rotation = Quaternion.LookRotation(cameraBearing);

            placementIndicator.transform.SetPositionAndRotation(placementPose.position, placementPose.rotation);
        }
        else
        {
            placementIndicator.SetActive(false);
        }
    }

    void UpdatePlacementPose()
    {
        var screenCenter = Camera.current.ViewportToScreenPoint(new Vector3(0.5f, 0.5f));
        var hits=new List<ARRaycastHit>();
        if (!placementModePick)
        {
            placementPoseIsValid = arRay.Raycast(screenCenter, hits, TrackableType.Planes);
            if (placementPoseIsValid)
            {
                placementPose = hits[0].pose;
            }
        }
        else
        {
            if(Input.touchCount>0&&Input.GetTouch(0).phase==TouchPhase.Began)
            {
                if (EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId))
                {
                    return;
                }
                placementIndicator.SetActive(false);
                Touch touch = Input.GetTouch(0);
                placementPoseIsValid = arRay.Raycast(touch.position, hits, TrackableType.Planes);
                if (placementPoseIsValid)
                {
                    placementPose = hits[0].pose;
                }
            }
        }
    }

    #endregion

    private void Update()
    {
        UpdatePlacementPose();
        UpdatePlacementIndicator();
        UpdateUI();
        debug.text = "originPos:" + aRSession.transform.position + "   " + "cameraPos:" + arCamera.transform.position + "\n" +
            "originRotation:" + aRSession.transform.rotation.eulerAngles + "   " + "cameraRotation:" + arCamera.transform.rotation.eulerAngles + "\n" +
            Vector3.Distance(prefab.transform.position, arCamera.transform.position) + "    " + new Vector3(-prefab.transform.position.x + arCamera.transform.position.x, 0, -prefab.transform.position.z + arCamera.transform.position.z)+"\n"+
            "Depth:"+Vector3.Dot(Camera.main.transform.forward,placementPose.position-Camera.main.transform.position);
        cube.transform.position = arCamera.transform.position + new Vector3(0, -0.5f, 0);
    }
}
