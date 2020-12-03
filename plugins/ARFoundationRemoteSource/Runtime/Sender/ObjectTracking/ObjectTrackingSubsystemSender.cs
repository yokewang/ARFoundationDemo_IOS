﻿#if ARFOUNDATION_4_0_OR_NEWER
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine.Assertions;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Debug = UnityEngine.Debug;


namespace ARFoundationRemote.Runtime {
    public class ObjectTrackingSubsystemSender : ISubsystemSender {
        [NotNull] readonly ARSessionOrigin origin;
        [CanBeNull] ARTrackedObjectManager manager;

        
        public ObjectTrackingSubsystemSender([NotNull] ARSessionOrigin origin) {
            this.origin = origin;
        }

        void createManager() {
            logCrash("createManager()");
            Assert.IsNull(manager);

            var go = origin.gameObject;
            var oldEnabled = go.activeSelf;
            go.SetActive(false);
            var newManager = go.AddComponent<ARTrackedObjectManager>();
            newManager.enabled = false;
            go.SetActive(oldEnabled);
            
            manager = newManager;
            newManager.trackedObjectsChanged += args => {
                var changes = new TrackableChangesData<XRTrackedObjectSerializable> {
                    added = serialize(args.added),
                    updated = serialize(args.updated),
                    removed = serialize(args.removed)
                };

                new PlayerToEditorMessage {
                    objectTrackingData = new ObjectTrackingData {
                        changes = changes
                    }
                }.Send();

                log(changes.ToString());
            };
        }

        [Conditional("_")]
        static void log(string message) {
            Debug.Log(message);
        }

        [NotNull]
        XRTrackedObjectSerializable[] serialize([NotNull] List<ARTrackedObject> list) {
            return list.Where(_ => {
                var isEmpty = _.referenceObject.guid == Guid.Empty;
                if (isEmpty) {
                    log("_.referenceObject.guid == Guid.Empty");                    
                }
                
                return !isEmpty;
            }).Select(XRTrackedObjectSerializable.Create).ToArray();
        }

        public void EditorMessageReceived(EditorToPlayerMessage data) {
            if (data.messageType.IsStop()) {
                disableObjectTracking();
                return;
            }
            
            var libraryContainer = data.objectTrackingData?.objectLibrary;
            if (libraryContainer.HasValue) {
                var maybeGuid = libraryContainer.Value.guid;
                if (maybeGuid.HasValue) {
                    var guid = maybeGuid.Value;
                    logCrash($"receive XRReferenceObjectLibrary {guid}");
                    var lib = ObjectTrackingLibraries.Instance.objectLibraries.SingleOrDefault(_ => _.guid == guid);
                    if (lib == null) {
                        logMissingObjRefLibError();
                        return;
                    }

                    if (manager == null) {
                        createManager();
                    }
                    
                    Assert.IsNotNull(manager);

                    var curLib = manager.referenceLibrary;
                    // setting same object library will produce empty guid objects
                    if (curLib == null || curLib.guid != guid) {
                        manager.referenceLibrary = lib;
                    }
                    
                    setManagerEnabled(true);
                                
                    var descriptor = manager.descriptor;
                    if (descriptor == null) {
                        DebugUtils.LogErrorOnce("- Object Tracking is not supported in this AR device.\nCurrently, only iOS devices with A12 chip or newer supports it.");
                    }
                } else {
                    disableObjectTracking();
                }
            }
        }

        void disableObjectTracking() {
            if (manager != null && manager.enabled) {
                logCrash("disableObjectTracking()");
                setManagerEnabled(false);
                
                // todo report _bug: setting referenceLibrary to null, then restoring the original referenceLibrary without reset will produce empty guid objects
                // manager.referenceLibrary = null;
            }
        }

        void setManagerEnabled(bool managerEnabled) {
            Sender.Instance.SetManagerEnabled(manager, managerEnabled);
        }

        [Conditional("_")]
        static void logCrash(string msg) {
            Sender.LogObjectTrackingCrash(msg);
        }

        public static void logMissingObjRefLibError() {
            Debug.LogError($"{Constants.packageName}: please add your Object Reference Library in plugin's Resources/ObjectTrackingLibraries and make a new build of AR Companion.");
        }
    }

    
    [Serializable]
    public struct ObjectTrackingData {
        public TrackableChangesData<XRTrackedObjectSerializable>? changes;
    }

    
    [Serializable]
    public struct XRTrackedObjectSerializable : ISerializableTrackable<XRTrackedObject> {
        TrackableIdSerializable trackableIdSerializable;
        PoseSerializable pose;
        TrackingState trackingState;
        Guid guid;


        public static XRTrackedObjectSerializable Create(ARTrackedObject _) {
            return new XRTrackedObjectSerializable {
                trackableIdSerializable = TrackableIdSerializable.Create(_.trackableId),
                pose = PoseSerializable.Create(_.transform.LocalPose()),
                trackingState = _.trackingState,
                guid = _.referenceObject.guid
            };
        }

        public TrackableId trackableId => trackableIdSerializable.Value;

        public XRTrackedObject Value => new XRTrackedObject(trackableId, pose.Value, trackingState, IntPtr.Zero, guid);
    }

    
    [Serializable]
    public struct ObjectLibraryContainer {
        public Guid? guid;
    }


    [Serializable]
    public struct ObjectTrackingDataEditor {
        public ObjectLibraryContainer? objectLibrary;
    }
}
#endif
