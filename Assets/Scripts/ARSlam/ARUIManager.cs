using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ARUIManager : MonoBehaviour
{
    public Canvas canvas;
    public Transform uiTag;
    public Raycast ray;

    public Dictionary<string, GameObject> uiPanel = new Dictionary<string, GameObject>();
    public GameObject optionsPanel;
    public GameObject controllerPanel;

    public Button openAndCloseForOptions;
    [SerializeField]
    private bool openOrCloseOptions;
    public Button openAndCloseForController;
    private bool openOrCloseController;

    private void Awake()
    {
        ray = GetComponent<Raycast>();
        uiPanel.Add("OptionsPanel", optionsPanel);
        uiPanel.Add("ControllerPanel", controllerPanel);
        openOrCloseOptions = true;
        openOrCloseController = true;
    }

    private void OnEnable()
    {
        openAndCloseForOptions.onClick.AddListener(OpenOrCloseOptions);
        openAndCloseForController.onClick.AddListener(OpenOrCloseController);
    }

    private void OnDisable()
    {
        openAndCloseForOptions.onClick.RemoveListener(OpenOrCloseOptions);
        openAndCloseForController.onClick.RemoveListener(OpenOrCloseController);
    }

    void Update()
    {
        if(ray.placementIndicator.activeSelf==false)
        {
            if(uiTag.gameObject.activeSelf==true)
            {
                uiTag.gameObject.SetActive(false);
            }
            return;
        }
        else
        {
            uiTag.gameObject.SetActive(true);
        }

        var pos = Camera.main.WorldToScreenPoint(ray.placementIndicator.transform.position);
        uiTag.transform.position = pos;
        pos = ray.placementIndicator.transform.position;
        pos.y -= ray.horRef;
        uiTag.GetComponentInChildren<Text>().text = pos.ToString();
    }

    class MoveArgs
    {
        public GameObject target;
        public Vector3 targetPos;
    }

    private IEnumerator MoveInAndOut(MoveArgs args)
    {
        while (Vector3.Distance( args.target.transform.position , args.targetPos)>0.01)
        {
            Vector3 pos = Vector3.Lerp(args.target.transform.position, args.targetPos, 0.5f);
            args.target.transform.position = pos;
            yield return null;
        }
        TransformHelper.FindChild(args.target.transform, "Open").Rotate(new Vector3(0, 0, 180));
    }


    private void OpenOrCloseOptions()
    {
        var target = uiPanel["OptionsPanel"];
        if (openOrCloseOptions)
        {
            var targetPos = target.transform.position + new Vector3(-500, 0, 0);
            StartCoroutine("MoveInAndOut", new MoveArgs{target=target,targetPos=targetPos });
        }
        else
        {
            var targetPos = target.transform.position + new Vector3(500, 0, 0);
            StartCoroutine("MoveInAndOut", new MoveArgs { target = target, targetPos = targetPos });
        }
        openOrCloseOptions = !openOrCloseOptions;
    }

    private void OpenOrCloseController()
    {
        var target = uiPanel["ControllerPanel"];
        if (openOrCloseController)
        {
            var targetPos = target.transform.position + new Vector3(0, -600, 0);
            StartCoroutine("MoveInAndOut", new MoveArgs { target = target, targetPos = targetPos });
        }
        else
        {
            var targetPos = target.transform.position + new Vector3(0, 600, 0);
            StartCoroutine("MoveInAndOut", new MoveArgs { target = target, targetPos = targetPos });
        }
        openOrCloseController = !openOrCloseController;
    }
}
