using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Debug = UnityEngine.Debug;


namespace ARFoundationRemote.Runtime {
    public class AnchorSubsystemSender : SubsystemSender {
        [SerializeField] ARAnchorManager manager = null;
        [SerializeField] ARPlaneManager planeManager = null;


        void OnEnable() {
            manager.anchorsChanged += anchorsChanged;
        }

        void OnDisable() {
            manager.anchorsChanged -= anchorsChanged;
        }

        void anchorsChanged(ARAnchorsChangedEventArgs args) {
            LogChangedAnchors(args);
            var payload = new TrackableChangesData<ARAnchorSerializable> {
                added = toSerializable(args.added),
                updated = toSerializable(args.updated),
                removed = toSerializable(args.removed)
            };
            
            new PlayerToEditorMessage{anchorSubsystemData = payload}.Send();
        }

        [Conditional("_")]
        public static void LogChangedAnchors(ARAnchorsChangedEventArgs args) {
            var added = args.added;
            if (added.Any()) {
                foreach (var anchor in added) {
                    log($"added {anchor.trackableId}");
                }
            }

            /*var updated = args.updated;
            if (updated.Any()) {
                foreach (var anchor in updated) {
                    print($"updated {anchor.trackableId}");
                }
            }*/

            var removed = args.removed;
            if (removed.Any()) {
                foreach (var anchor in removed) {
                    log($"removed {anchor.trackableId}");
                }
            }
        }

        ARAnchorSerializable[] toSerializable(List<ARAnchor> anchors) {
            return anchors.Select(ARAnchorSerializable.Create).ToArray();
        }

        public override void EditorMessageReceived(EditorToPlayerMessage data) {
            var maybeAnchorsData = data.anchorsData;
            if (maybeAnchorsData.HasValue) {
                var anchorsData = maybeAnchorsData.Value;
                
                if (anchorsData.enableManager.HasValue) {
                    Sender.Instance.SetManagerEnabled(manager, anchorsData.enableManager.Value);
                    return;
                }

                Assert.IsTrue(data.requestGuid.HasValue);
                var requestGuid = data.requestGuid.Value;

                var tryAddAnchorData = anchorsData.tryAddAnchorData;
                if (tryAddAnchorData.HasValue) {
                    var anchor = addAnchor(tryAddAnchorData.Value.pose.Value);
                    sendMethodResponse(anchor, anchor != null, requestGuid);
                    return;
                }
                
                var tryAttachAnchorData = anchorsData.tryAttachAnchorData;
                if (tryAttachAnchorData.HasValue) {
                    var attachAnchorData = tryAttachAnchorData.Value;
                    var plane = planeManager.GetPlane(attachAnchorData.trackableToAffix.Value);
                    Assert.IsNotNull(plane);
                    var anchor = attachAnchor(plane, attachAnchorData.pose.Value);
                    if (anchor != null) {
                        log($"anchor {anchor.trackableId} attached to plane {plane.trackableId}");
                        LogAllTrackables(manager);
                    }
                    
                    sendMethodResponse(anchor, anchor != null, requestGuid);
                    return;
                }
                
                var tryRemoveAnchorData = anchorsData.tryRemoveAnchorData;
                if (tryRemoveAnchorData.HasValue) {
                    sendMethodResponse(null,tryRemoveAnchor(tryRemoveAnchorData.Value.anchorId.Value), requestGuid);
                    return;
                }

                throw new Exception();
            }
        }

        bool tryRemoveAnchor(TrackableId id) {
            var anchor = manager.GetAnchor(id);
            if (anchor == null) {
                Debug.LogError($"GetAnchor == null {id}");
                LogAllTrackables(manager);
                return false;
            }

            var removed = manager.RemoveAnchor(anchor);
            if (!removed) {
                Debug.LogError($"RemoveAnchor failed {id}");
            } else {
                log($"anchor removed {anchor.trackableId}");
                LogAllTrackables(manager);
            }
            
            return removed;
        }

        [Conditional("_")]
        static void log(string s) {
            Debug.Log(s);
        }

        [Conditional("_")]
        public static void LogAllTrackables(ARAnchorManager m) {
            log("\nALL TRACKABLES ");
            foreach (var trackable in m.trackables) {
                print(trackable.trackableId);
            }
        }
        
        [CanBeNull]
        ARAnchor addAnchor(Pose pose) {
            return manager.AddAnchor(pose);
        }

        static void sendMethodResponse([CanBeNull] ARAnchor anchor, bool isSuccess, Guid responseGuid) {
            new PlayerToEditorMessage {
                anchorSubsystemMethodsResponse = new AnchorSubsystemMethodsResponse {
                    anchor = ARAnchorSerializable.CreateIfNotNull(anchor),
                    isSuccess = isSuccess
                },
                responseGuid = responseGuid
            }.Send();
        }

        [CanBeNull]
        ARAnchor attachAnchor(ARPlane plane, Pose poseValue) {
            return manager.AttachAnchor(plane, poseValue);
        }
    }
    
    [Serializable]
    public struct ARAnchorSerializable : ISerializableTrackable<XRAnchor> {
        TrackableIdSerializable trackableIdSer;
        PoseSerializable pose;
        TrackingState trackingState;
        IntPtr nativePtr;
        Guid sessionId;


        public static ARAnchorSerializable? CreateIfNotNull([CanBeNull] ARAnchor a) {
            if (a != null) {
                return Create(a);
            } else {
                return null;
            }
        }

        public static ARAnchorSerializable Create([NotNull] ARAnchor a) {
            return new ARAnchorSerializable {
                trackableIdSer = TrackableIdSerializable.Create(a.trackableId),
                pose = PoseSerializable.Create(a.transform.LocalPose()),
                trackingState = a.trackingState,
                nativePtr = a.nativePtr,
                sessionId = a.sessionId,
            };
        }

        public TrackableId trackableId => trackableIdSer.Value;
        public XRAnchor Value => new XRAnchor(trackableId, pose.Value, trackingState, nativePtr, sessionId);
    }

    [Serializable]
    public struct AnchorSubsystemMethodsResponse {
        public ARAnchorSerializable? anchor;
        public bool isSuccess;
    }

    [Serializable]
    public struct AnchorDataEditor {
        public TryAddAnchorData? tryAddAnchorData;
        public TryAttachAnchorData? tryAttachAnchorData;
        public TryRemoveAnchorData? tryRemoveAnchorData;
        public bool? enableManager;
    }

    [Serializable]
    public struct TryAddAnchorData {
        public PoseSerializable pose;
    }

    [Serializable]
    public struct TryAttachAnchorData {
        public TrackableIdSerializable trackableToAffix;
        public PoseSerializable pose;
    }

    [Serializable]
    public struct TryRemoveAnchorData {
        public TrackableIdSerializable anchorId;
    }
}
