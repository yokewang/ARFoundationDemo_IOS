using ARFoundationRemote.Runtime;
using UnityEngine;
using UnityEngine.XR.ARFoundation;


namespace ARFoundationRemoteExample.Runtime {
    public class ARKitObjectTrackingExample : MonoBehaviour {
        void Awake() {
            #if ARFOUNDATION_4_0_OR_NEWER
                if (FindObjectOfType<ARTrackedObjectManager>().referenceLibrary == null) {
                    Debug.LogError(
                        $"{Constants.packageName}: please set the ARTrackedObjectManager.referenceLibrary in inspector, " +
                        $"add your image library to plugin's 'Assets/Plugins/ARFoundationRemoteInstaller/Resources/ObjectTrackingLibraries', and make a new build of AR Companion app.");
                }
            #endif
        }
    }
}
