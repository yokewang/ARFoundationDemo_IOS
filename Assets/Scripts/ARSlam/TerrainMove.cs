using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TerrainMove : MonoBehaviour
{
    public Transform terrainTransform;

    //用于获得相机的方位用于计算当前位置
    public Camera arCamera;

    void Update()
    {
        if(terrainTransform.gameObject.activeInHierarchy!=true)
        {
            return;
        }
        else
        {
            //当前相机的位置和地形中心的距离，通过这个距离来调整地形的位置
            //如果左右的距离发生变化
            var dis = arCamera.transform.position.x-terrainTransform.position.x;
            if (dis > 1)
            {
                terrainTransform.Translate((int)dis * new Vector3(1f, 0, 0));
            }
            else if(dis<-1)
            {
                terrainTransform.Translate((int)dis * new Vector3(1f, 0, 0));
            }

            //前后距离发生变化
            dis = arCamera.transform.position.z - terrainTransform.position.z;
            if (dis > 1)
            {
                terrainTransform.Translate((int)dis * new Vector3(0, 0, 1f));
            }
            else if (dis < -1)
            {
                terrainTransform.Translate((int)dis * new Vector3(0, 0, 1f));
            }

        }
    }
}
