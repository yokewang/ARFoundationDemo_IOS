using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.XR.ARFoundation;


namespace ARFoundationRemote.Runtime {
    public class CameraPoseSender : MonoBehaviour {
        void Awake() {
            if (Application.isEditor) {
                Debug.LogError(GetType().Name + " is written for running on device, not in Editor");
                enabled = false;
                return;
            }
        }

        void LateUpdate() {
            if (Sender.Instance.IsConnectedAndRunning) {
                new PlayerToEditorMessage {cameraPose = PoseSerializable.Create(transform.LocalPose())}.Send();
            }
        }
    }
}
