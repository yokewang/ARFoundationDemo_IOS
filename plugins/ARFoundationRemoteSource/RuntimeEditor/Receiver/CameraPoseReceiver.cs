#if UNITY_EDITOR
using ARFoundationRemote.Runtime;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.ARFoundation;


namespace ARFoundationRemote.RuntimeEditor {
    public class CameraPoseReceiver : IReceiver {
        [CanBeNull] ARSessionOrigin _origin;


        [CanBeNull]
        ARSessionOrigin getOrigin() {
            if (_origin == null) {
                var foundInScene = Object.FindObjectOfType<ARSessionOrigin>();
                _origin = foundInScene;

                /*if (foundInScene != null) {
                    Debug.Log("origin cached ", _origin);
                }*/
            }

            return _origin;
        }

        public void Receive(PlayerToEditorMessage data) {
            var maybeCameraPose = data.cameraPose;
            if (!maybeCameraPose.HasValue) {
                return;
            }

            var origin = getOrigin();
            if (origin == null) {
                return;
            }
            
            var cam = origin.camera;
            if (cam != null && ARSession.state >= ARSessionState.SessionInitializing && !EditorApplication.isPaused) {
                var arCameraTransform = cam.transform;
                var pose = maybeCameraPose.Value.Value;
                arCameraTransform.localPosition = pose.position;
                arCameraTransform.localRotation = pose.rotation;
            }
        }
    }
}
#endif
