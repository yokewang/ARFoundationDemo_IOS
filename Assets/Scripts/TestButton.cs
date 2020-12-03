using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class TestButton : MonoBehaviour,IPointerDownHandler, IPointerUpHandler
{
    Button button;
    bool isDown;
    float time;

    // Start is called before the first frame update
    void Start()
    {
        button = GetComponent<Button>();
        isDown = false;
    }

    // Update is called once per frame
    void Update()
    {
        if(isDown)
        {
            time += Time.deltaTime;
            Debug.Log(time);
        }
    }

    public void OnPointerDown(PointerEventData data)
    {
        isDown = true;
        time = 0;
    }

    public void OnPointerUp(PointerEventData data)
    {
        isDown = false;
    }


}
