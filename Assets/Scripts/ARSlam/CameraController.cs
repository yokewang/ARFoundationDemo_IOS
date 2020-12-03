using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.ARFoundation;
using UnityEngine.UI;

public class CameraController : MonoBehaviour
{
    //实际控制的目标
    public ARSessionOrigin arSessionOrigin;

    //方法1.控制模型移动
    //public Transform root;

    //模型物体
    public GameObject terrain;

    //保存临时轴
    public Transform temproot;

    //用于获得按钮
    public Canvas uiRoot;
    public GameObject uiPanel;

    public bool rotateSelf = false;
    public Button rotateSelfButton;

    public float moveSpeed;
    public float rotateSpeed;
    public float upDownSpeed;

    //用于获得相机的方位用于计算移动的方向
    public Camera arCamera;
    //用于获得旋转的中心
    public Raycast ray;

    [SerializeField]
    private Quaternion angle = Quaternion.identity;

    #region 初始化
    void Awake()
    {
        arSessionOrigin = FindObjectOfType<ARSessionOrigin>();
        uiRoot = FindObjectOfType<Canvas>();
        uiPanel = uiRoot.transform.Find("ControllerPanel").gameObject;
        ray = FindObjectOfType<Raycast>();
        rotateSelfButton = TransformHelper.FindChild( uiRoot.transform,"RotateSelf").GetComponent<Button>();
        angle = arSessionOrigin.transform.rotation;
    }

    private void OnEnable()
    {
        Init();
    }

    private void OnDisable()
    {
        CancelInit();
    }
    #endregion

    #region 相机初始化
    void Init()
    {
        TransformHelper.FindChild(uiPanel.transform, "前").GetComponent<Button>().onClick.AddListener(MoveForward);
        TransformHelper.FindChild(uiPanel.transform,"后").GetComponent<Button>().onClick.AddListener(MoveBackward);
        TransformHelper.FindChild(uiPanel.transform,"左").GetComponent<Button>().onClick.AddListener(MoveLeft);
        TransformHelper.FindChild(uiPanel.transform,"右").GetComponent<Button>().onClick.AddListener(MoveRight);

        TransformHelper.FindChild(uiPanel.transform,"左").GetComponent<Button>().onClick.AddListener(TurnLeft);
        TransformHelper.FindChild(uiPanel.transform,"右").GetComponent<Button>().onClick.AddListener(TurnRight);
        TransformHelper.FindChild(uiPanel.transform,"俯").GetComponent<Button>().onClick.AddListener(TurnDown);
        TransformHelper.FindChild(uiPanel.transform,"仰").GetComponent<Button>().onClick.AddListener(TurnUp);

        TransformHelper.FindChild(uiPanel.transform,"高").GetComponent<Button>().onClick.AddListener(MoveUp);
        TransformHelper.FindChild(uiPanel.transform,"低").GetComponent<Button>().onClick.AddListener(MoveDown);

        TransformHelper.FindChild(uiRoot.transform,"RotateSelf").GetComponent<Button>().onClick.AddListener(ChangeRotateMode);
    }

    void CancelInit()
    {
        TransformHelper.FindChild(uiPanel.transform, "前").GetComponent<Button>().onClick.RemoveListener(MoveForward);
        TransformHelper.FindChild(uiPanel.transform, "后").GetComponent<Button>().onClick.RemoveListener(MoveBackward);
        TransformHelper.FindChild(uiPanel.transform, "左").GetComponent<Button>().onClick.RemoveListener(MoveLeft);
        TransformHelper.FindChild(uiPanel.transform, "右").GetComponent<Button>().onClick.RemoveListener(MoveRight);

        TransformHelper.FindChild(uiPanel.transform, "左").GetComponent<Button>().onClick.RemoveListener(TurnLeft);
        TransformHelper.FindChild(uiPanel.transform, "右").GetComponent<Button>().onClick.RemoveListener(TurnRight);
        TransformHelper.FindChild(uiPanel.transform, "俯").GetComponent<Button>().onClick.RemoveListener(TurnDown);
        TransformHelper.FindChild(uiPanel.transform, "仰").GetComponent<Button>().onClick.RemoveListener(TurnUp);

        TransformHelper.FindChild(uiPanel.transform, "高").GetComponent<Button>().onClick.RemoveListener(MoveUp);
        TransformHelper.FindChild(uiPanel.transform, "低").GetComponent<Button>().onClick.RemoveListener(MoveDown);

        TransformHelper.FindChild(uiRoot.transform, "RotateSelf").GetComponent<Button>().onClick.RemoveListener(ChangeRotateMode);
    }
    #endregion

    #region 相机控制移动
    public void MoveForward()
    {
#if UNITY_EDITOR
        Debug.Log("前进");
#endif
        //获取相机朝向，让物体按照相机朝向的方向移动，其实是相机按照相反方向移动，但是在makecontent方法中以及做了方向变换，如果直接移动需要反向
        var dir = arCamera.transform.forward;
        //不需要的y分量，不需要相机在y轴方向有偏转
        dir.y = 0;
        //标准化，可以屏蔽相机角度的影响，可以获得一致性的效果，屏蔽后根据视角变化可以进行精细调整
        dir = dir.normalized;

        //直接移动arsessionorigin导致相机旋转
        //arSessionOrigin.transform.Translate(dir * moveSpeed,Space.World);

        arSessionOrigin.MakeContentAppearAt(terrain.transform, terrain.transform.position + dir * moveSpeed);
    }

    public void MoveBackward()
    {
#if UNITY_EDITOR
        Debug.Log("后退");
#endif
        //获取相机朝向，让物体按照相机朝向的方向移动，其实是相机按照相反方向移动，但是在makecontent方法中以及做了方向变换，如果直接移动需要反向
        var dir = -arCamera.transform.forward;
        //不需要的y分量，不需要相机在y轴方向有偏转
        dir.y = 0;
        //标准化，可以屏蔽相机角度的影响，可以获得一致性的效果，屏蔽后根据视角变化可以进行精细调整
        dir = dir.normalized;

        //直接移动arsessionorigin导致相机旋转
        //arSessionOrigin.transform.Translate(dir * moveSpeed, Space.World);

        //
        arSessionOrigin.MakeContentAppearAt(terrain.transform, terrain.transform.position + dir * moveSpeed);
    }

    public void MoveLeft()
    {
#if UNITY_EDITOR
        Debug.Log("向左");
#endif
        //获取相机朝向，让物体按照相机朝向的方向移动，其实是相机按照相反方向移动，但是在makecontent方法中以及做了方向变换，如果直接移动需要反向
        var dir = -arCamera.transform.right;
        //不需要的y分量，不需要相机在y轴方向有偏转
        dir.y = 0;
        //标准化，可以屏蔽相机角度的影响，可以获得一致性的效果，屏蔽后根据视角变化可以进行精细调整
        dir = dir.normalized;

        //直接移动arsessionorigin导致相机旋转
        //arSessionOrigin.transform.Translate(dir * moveSpeed, Space.World);

        arSessionOrigin.MakeContentAppearAt(terrain.transform, terrain.transform.position + dir * moveSpeed);
    }

    public void MoveRight()
    {
#if UNITY_EDITOR
        Debug.Log("向右");
#endif
        //获取相机朝向，让物体按照相机朝向的方向移动，其实是相机按照相反方向移动，但是在makecontent方法中以及做了方向变换，如果直接移动需要反向
        var dir = arCamera.transform.right;
        //不需要的y分量，不需要相机在y轴方向有偏转
        dir.y = 0;
        //标准化，可以屏蔽相机角度的影响，可以获得一致性的效果，屏蔽后根据视角变化可以进行精细调整
        dir = dir.normalized;

        //直接移动arsessionorigin导致相机旋转
        //arSessionOrigin.transform.Translate(dir * moveSpeed, Space.World);

        arSessionOrigin.MakeContentAppearAt(terrain.transform, terrain.transform.position + dir * moveSpeed);
    }
    #endregion

    #region 相机控制旋转
    public void ChangeRotateMode()
    {
        rotateSelf = !rotateSelf;
        if(rotateSelf)
        {
            rotateSelfButton.GetComponentInChildren<Text>().text = "Local";
        }
        else
        {
            rotateSelfButton.GetComponentInChildren<Text>().text = "Global";
        }
        
    }

    public void TurnLeft()
    {
#if UNITY_EDITOR
        Debug.Log("向左转");
#endif
        if(!rotateSelf)
        {
            //直接旋转
            //arSessionOrigin.transform.RotateAround(ray.placementPose.position, ray.placementPose.up, 10 * rotateSpeed);

            //计算旋转中心
            temproot.position = ray.placementPose.position;
            var center = new Vector3(temproot.position.x, 0, temproot.position.z);

            //把arsessionorigin移动到需要旋转的地方
            //arSessionOrigin.MakeContentAppearAt(ray.placementIndicator.transform, ray.placementIndicator.transform.position);
            arSessionOrigin.MakeContentAppearAt(temproot.transform, temproot.transform.position);

            //转换坐标系
            angle = angle * Quaternion.Euler(new Vector3(0, -rotateSpeed, 0));
            arSessionOrigin.MakeContentAppearAt(terrain.transform.parent, angle);
        }
        else
        {
            //直接旋转
            //var axis = new Vector3(0, arSessionOrigin.transform.up.y, 0).normalized;
            //arSessionOrigin.transform.RotateAround(arCamera.transform.position, axis, 10 * rotateSpeed);

            //把arsessionorigin移动到需要旋转的地方
            temproot.position = arCamera.transform.position;
            arSessionOrigin.MakeContentAppearAt(temproot, temproot.position);

            angle = angle * Quaternion.Euler(new Vector3(0, rotateSpeed, 0));
            arSessionOrigin.MakeContentAppearAt(terrain.transform.parent, angle);

        }
    }

    public void TurnRight()
    {
#if UNITY_EDITOR
        Debug.Log("向右转");
#endif
        if (!rotateSelf)
        {
            //直接旋转
            //arSessionOrigin.transform.RotateAround(ray.placementPose.position, ray.placementPose.up, -10 * rotateSpeed);

            //计算旋转中心
            temproot.position = ray.placementPose.position;
            var center = new Vector3(temproot.position.x, 0, temproot.position.z);

            //把arsessionorigin移动到需要旋转的地方
            //arSessionOrigin.MakeContentAppearAt(ray.placementIndicator.transform, ray.placementIndicator.transform.position);
            arSessionOrigin.MakeContentAppearAt(temproot.transform, temproot.transform.position);

            //转换坐标系
            angle = angle * Quaternion.Euler(new Vector3(0, rotateSpeed, 0));
            arSessionOrigin.MakeContentAppearAt(terrain.transform.parent, angle);
        }
        else
        {
            //直接旋转
            //var axis = new Vector3(0, arSessionOrigin.transform.up.y, 0).normalized;
            //arSessionOrigin.transform.RotateAround(arCamera.transform.position, axis, -10 * rotateSpeed);

            //把arsessionorigin移动到需要旋转的地方
            temproot.position = arCamera.transform.position;
            arSessionOrigin.MakeContentAppearAt(temproot, temproot.position);

            angle = angle * Quaternion.Euler(new Vector3(0, -rotateSpeed, 0));
            arSessionOrigin.MakeContentAppearAt(terrain.transform.parent, angle);
        }
    }

    public void TurnUp()
    {
#if UNITY_EDITOR
        Debug.Log("后仰");
#endif
        if (!rotateSelf)
        {
            //arSessionOrigin.transform.Rotate(ray.placementPose.up, 10 * speed,Space.World);
            arSessionOrigin.transform.RotateAround(ray.placementPose.position, ray.placementPose.right, -10 * rotateSpeed);
        }
        else
        {
            arSessionOrigin.transform.Rotate(arCamera.transform.right, -10 * rotateSpeed);
        }
       
    }


    public void TurnDown()
    {
#if UNITY_EDITOR
        Debug.Log("前俯");
#endif
        if (!rotateSelf)
        {
            //直接旋转
            arSessionOrigin.transform.RotateAround(ray.placementPose.position, ray.placementPose.right, 10 * rotateSpeed);
        }
        else
        {
            arSessionOrigin.transform.Rotate(arCamera.transform.right, 10 * rotateSpeed);

        }
    }

    #endregion

    #region 相机控制提升

    public void MoveUp()
    {
#if UNITY_EDITOR
        Debug.Log("提升");
#endif
        //直接提升
        //arSessionOrigin.transform.Translate(new Vector3(0, -1, 0) * upDownSpeed, Space.World);

        //实际提升物体需要下降相机，使用y的负方向作为方向
        arSessionOrigin.MakeContentAppearAt(terrain.transform, terrain.transform.position + new Vector3(0, 1, 0) * upDownSpeed);
    }

    public void MoveDown()
    {
#if UNITY_EDITOR
        Debug.Log("下降");
#endif
        //直接下降
        //arSessionOrigin.transform.Translate(new Vector3(0, 1, 0) * upDownSpeed, Space.World);

        //实际提升物体需要提升相机，使用y的正方向作为方向
        arSessionOrigin.MakeContentAppearAt(terrain.transform, terrain.transform.position + new Vector3(0, -1, 0) * upDownSpeed);
    }
    #endregion

}
