﻿using ARFoundationRemote.Runtime;
using UnityEngine;


namespace ARFoundationRemoteExample.Runtime {
    public class CheckUguiInstalled : MonoBehaviour {
        void Awake() {
            if (!Defines.isCanvasGUIInstalled) {
                Debug.LogError($"{Constants.packageName}: please install Unity UI (\"com.unity.ugui\") via Package Manager to run this example");
            }
        }
    }
}
