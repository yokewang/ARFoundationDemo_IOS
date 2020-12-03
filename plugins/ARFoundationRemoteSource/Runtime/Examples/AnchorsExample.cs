using System;
using System.Collections.Generic;
using System.Linq;
using ARFoundationRemote.Runtime;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
#if UNITY_2019_2 && AR_FOUNDATION_REMOTE_INSTALLED
    using Input = ARFoundationRemote.Input;
#else
    using Input = UnityEngine.Input;
#endif


namespace ARFoundationRemoteExample.Runtime {
    public class AnchorsExample : MonoBehaviour {
        [SerializeField] ARAnchorManager anchorManager = null;
        [SerializeField] ARRaycastManager raycastManager = null;
        [SerializeField] ARSessionOrigin origin = null;
        [SerializeField] ARPlaneManager planeManager = null;
        [SerializeField] TrackableType raycastMask = TrackableType.PlaneWithinPolygon;

        AnchorTestType type = AnchorTestType.Add;


        void OnEnable() {
            anchorManager.anchorsChanged += anchorsChanged;
        }

        void OnDisable() {
            anchorManager.anchorsChanged -= anchorsChanged;
        }

        void anchorsChanged(ARAnchorsChangedEventArgs args) {
            AnchorSubsystemSender.LogChangedAnchors(args);
        }

        void Update() {
            for (int i = 0; i < Input.touchCount; i++) {
                var touch = Input.GetTouch(i);
                if (touch.phase != TouchPhase.Began) {
                    continue;
                }
                
                var ray = origin.camera.ScreenPointToRay(touch.position);
                var hits = new List<ARRaycastHit>();
                var hasHit = raycastManager.Raycast(ray, hits, raycastMask);
                if (hasHit) {
                    switch (type) {
                        case AnchorTestType.Add: {
                            var anchor = anchorManager.AddAnchor(hits.First().pose);
                            print($"anchor added: {anchor != null}");
                            break;
                        }
                        case AnchorTestType.AttachToPlane: {
                            var attachedToPlane = tryAttachToPlane(hits);
                            print($"anchor attached successfully: {attachedToPlane}");
                            break;
                        }
                        default:
                            throw new Exception();
                    }
                } else {
                    // print("no hit");
                }
            }
        }

        bool tryAttachToPlane(List<ARRaycastHit> hits) {
            foreach (var hit in hits) {
                var plane = planeManager.GetPlane(hit.trackableId);
                if (plane != null) {
                    var anchor = anchorManager.AttachAnchor(plane, hit.pose);
                    if (anchor != null) {
                        return true;
                    }
                }
            }

            return false;
        }

        void OnGUI() {
            #if AR_FOUNDATION_REMOTE_INSTALLED
            if (GUI.Button(new Rect(0,0,400,200), $"Current type: {type}")) {
                type = type == AnchorTestType.Add ? AnchorTestType.AttachToPlane : AnchorTestType.Add;
            }

            if (GUI.Button(new Rect(0, 200, 400, 200), "Remove all anchors")) {
                var copiedAnchors = new HashSet<ARAnchor>();
                foreach (var _ in anchorManager.trackables) {
                    copiedAnchors.Add(_);
                }
                
                foreach (var _ in copiedAnchors) {
                    anchorManager.RemoveAnchor(_);
                }
            }
            #endif
        }

        enum AnchorTestType {
            Add,
            AttachToPlane
        }
    }
}
