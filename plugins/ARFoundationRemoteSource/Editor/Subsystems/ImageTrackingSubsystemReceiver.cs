using System.Diagnostics;
using ARFoundationRemote.Runtime;
using ARFoundationRemote.RuntimeEditor;
using JetBrains.Annotations;
using Unity.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Debug = UnityEngine.Debug;


namespace ARFoundationRemote.Editor {
    public partial class ImageTrackingSubsystem : IReceiver, IOnUpdate {
        static readonly TrackableChangesReceiver<XRTrackedImageSerializable, XRTrackedImage> receiver = new TrackableChangesReceiver<XRTrackedImageSerializable, XRTrackedImage>();

        [CanBeNull] ARTrackedImageManager _manager;

        [CanBeNull]
        ARTrackedImageManager manager {
            get {
                if (_manager == null) {
                    _manager = Object.FindObjectOfType<ARTrackedImageManager>();
                }

                return _manager;
            }
        }
        
        bool? managerEnabled;


        void IReceiver.Receive(PlayerToEditorMessage data) {
            receiver.Receive(data.trackedImagesData);
        }

        static TrackableChanges<XRTrackedImage> getChanges(Allocator allocator) {
            return receiver.GetChanges(allocator);
        }

        void IOnUpdate.OnUpdate() {
            if (manager == null) {
                return;
            }
            
            var curEnabled = manager.enabled;
            if (managerEnabled != curEnabled) {
                managerEnabled = curEnabled;
                log($"send {nameof(ARTrackedImageManager)} enabled: " + curEnabled);
                new EditorToPlayerMessage {enableImageTracking = curEnabled}.Send();
            }
        }
        
        [Conditional("_")]
        static void log(string msg) {
            Debug.Log($"{nameof(ImageTrackingSubsystem)}: {msg}");
        }
    }
}
