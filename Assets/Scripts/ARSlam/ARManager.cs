using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.UI;

public class ARManager : MonoBehaviour
{
    [SerializeField]
    ARSessionOrigin arOrigin;
    [SerializeField]
    ARTrackedImageManager arTrackedImageManager;
    public Transform arPrefabsRoot;
    public Text alert;
    public Text imageName;
    public GameObject prefab;


    [SerializeField]
    [Tooltip("The camera to set on the world space UI canvas for each instantiated image info.")]
    Camera m_WorldSpaceCanvasCamera;

    public Camera worldSpaceCanvasCamera
    {
        get { return m_WorldSpaceCanvasCamera; }
        set { m_WorldSpaceCanvasCamera = value; }
    }

    private void Awake()
    {
        arOrigin = FindObjectOfType<ARSessionOrigin>();
        arTrackedImageManager = arOrigin.GetComponent<ARTrackedImageManager>();
    }

    private void OnEnable()
    {
        arTrackedImageManager.trackedImagesChanged += OnTrackedImagesChanged;
    }

    private void OnDisable()
    {
        arTrackedImageManager.trackedImagesChanged -= OnTrackedImagesChanged;
    }

    public void UpdateInfo(ARTrackedImage trackedImage)
    {
        alert.text = "Detected images changed!";
        imageName.text = trackedImage.referenceImage.name;

        Canvas canvas = trackedImage.GetComponentInChildren<Canvas>();
        canvas.worldCamera = worldSpaceCanvasCamera;

        Text tag = canvas.GetComponentInChildren<Text>();
        tag.text = string.Format(
            "{0}\ntrackingState: {1}\nGUID: {2}\nReference size: {3}cm\nDetected size: {4}cm",
        trackedImage.referenceImage.name,
        trackedImage.trackingState,
        trackedImage.referenceImage.guid,
        trackedImage.referenceImage.size * 100f,
        trackedImage.size * 100f);

        var planeParentGo = trackedImage.transform.GetChild(0).gameObject;
        var planeGo = planeParentGo.transform.GetChild(0).gameObject;

        if (trackedImage.trackingState != TrackingState.None)
        {
            planeGo.SetActive(true);

            trackedImage.transform.localScale = new Vector3(trackedImage.size.x, 1f, trackedImage.size.y);

            var material = planeGo.GetComponentInChildren<MeshRenderer>().material;
            material.mainTexture = (trackedImage.referenceImage.texture == null) ? null : trackedImage.referenceImage.texture;
        }
        else
        {
            planeGo.SetActive(false);

        }
    }

    void OnTrackedImagesChanged(ARTrackedImagesChangedEventArgs eventArgs)
    {
        foreach (var trackedImage in eventArgs.added)
        {
            // Give the initial image a reasonable default scale
            //trackedImage.transform.localScale = new Vector3(0.01f, 1f, 0.01f);
            UpdateInfo(trackedImage);
        }

        foreach (var trackedImage in eventArgs.updated)
            UpdateInfo(trackedImage);
    }
}
