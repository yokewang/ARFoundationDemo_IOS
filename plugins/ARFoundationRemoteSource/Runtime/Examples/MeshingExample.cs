using ARFoundationRemote.Runtime;
using UnityEngine;
using UnityEngine.XR.ARFoundation;


namespace ARFoundationRemoteExample.Runtime {
    public class MeshingExample : MonoBehaviour {
        [SerializeField] ARMeshManager manager = null;
        [SerializeField] bool showMeshDestructionButton = false;
        
        
        void Awake() {
            #if UNITY_IOS && !(ARKIT_INSTALLED && ARFOUNDATION_4_0_OR_NEWER)
                Debug.LogError($"{Constants.packageName}: please install ARKit XR Plugin >= 4.0");
            #endif
            
            #if UNITY_ANDROID
                Debug.LogError($"{Constants.packageName}: meshing is not supported by ARCore");
            #endif
        }
        
        void OnGUI() {
            #if AR_FOUNDATION_REMOTE_INSTALLED
            if (showMeshDestructionButton && GUI.Button(new Rect(0,0,400,200), "DestroyAllMeshes")) {
                manager.DestroyAllMeshes();
            }
            #endif
        }
    }
}
