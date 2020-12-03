using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameObjectPool:Mono_Singleton<GameObjectPool>
{
    //1.创建池
    private Dictionary<string, List<GameObject>> cache=new Dictionary<string, List<GameObject>>();

    //2.创建使用池的元素
    public GameObject CreateObject(string key,GameObject go,Vector3 position, Quaternion quateration)
    {
        //1.查找池中有无可用对象
        GameObject tempGo = FindUsable(key);
        //2.有，返回
        if (tempGo != null)
        {
            tempGo.transform.position = position;
            tempGo.transform.rotation = quateration;
            tempGo.SetActive(true);
        }
        else //3.无，加载
        {
            tempGo = Instantiate(go, position, quateration) as GameObject;
            //放入池中
            Add(key,tempGo);
        }

        tempGo.transform.parent = this.transform;
        return tempGo;

    }

    public void Clear(string key)
    {
        if (cache.ContainsKey(key))
        {
            //释放场景中的游戏物体
            for(int i = 0; i < cache[key].Count; i++)
            {
                Destroy(cache[key][i]);
            }
            //移除了对象的地址
            cache.Remove(key);
        }
    }

    public void ClaerAll()
    {
        foreach(string key in cache.Keys)
        {
            Clear(key);
        }
    }

    public void CollectGameObejct(GameObject go)
    {
        if (go != null)
        {
            go.SetActive(false);
        }
        else
        {
            Debug.Log("No item");
        }
    }

    public void CollectGameObejct(GameObject go, float delay)
    {
        StartCoroutine(CollectDelay(go,delay));
    }

    private IEnumerator CollectDelay(GameObject go,float delay)
    {
        yield return new WaitForSeconds(delay);
        CollectGameObejct(go);
    }

    private GameObject FindUsable(string key)
    {
        if (cache.ContainsKey(key))
        {
            return cache[key].Find(p => !p.activeSelf);
        }
        return null;
    }

    private void Add(string key, GameObject go)
    {
        if (cache.ContainsKey(key))
        {
            cache[key].Add(go);
        }
        else
        {
            cache.Add(key, new List<GameObject>());
            cache[key].Add(go);
        }
    }
}
