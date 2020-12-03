﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using JetBrains.Annotations;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Debug = UnityEngine.Debug;
#if (UNITY_IOS || UNITY_EDITOR) && ARFOUNDATION_REMOTE_ENABLE_IOS_BLENDSHAPES
    using UnityEngine.XR.ARKit;
#endif


namespace ARFoundationRemote.Runtime {
    [RequireComponent(typeof(ARSessionOrigin))]
    public class FaceSubsystemSender : SubsystemSender {
        [SerializeField] ARFaceManager manager = null;

        float lastSendTime;
        FaceTrackingStateData[] prevStates = new FaceTrackingStateData[0];
        static bool faceTrackingSupportChecked;
        

        void Awake() {
            if (Application.isEditor) {
                Debug.LogError(GetType().Name + " is written for running on device, not in Editor");
            }

            manager.facesChanged += onChanges;
        }

        void OnDestroy() {
            manager.facesChanged -= onChanges;
        }

        IEnumerator checkARKitFaceTrackingPlugin() {
            while (ARSession.state <= ARSessionState.Ready) {
                yield return null;
            }

            while (!manager.enabled) {
                yield return null;
            }

            if (manager.descriptor == null) {
                Sender.runningErrorMessage += "- Face tracking is not supported:\n" +
                                              "please install ARKit Face Tracking via Package Manager\n" +
                                              "AND enable 'Face Tracking' in ARKit loader\n";
            }
        }

        void onChanges(ARFacesChangedEventArgs args) {
            if (shouldSend(args) || Connection.senderConnection.CanSendNonCriticalMessage && checkCurrentFPS()) {
                var data = new FaceSubsystemData(toSerializable(args.added), toSerializable(args.updated), toSerializable(args.removed));
                if (data.needLogFaces) {
                    log("send faces\n" + data);
                }

                data.uniqueData = trySerializeUniqueData(args.added);
                new PlayerToEditorMessage {faceSubsystemData = data}.Send();
            }
        }

        readonly HashSet<TrackableId> uniqueDataIds = new HashSet<TrackableId>();

        [CanBeNull]
        [SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
        FaceUniqueData[] trySerializeUniqueData([NotNull] List<ARFace> argsAdded) {
            var newFaces = argsAdded.Where(_ => !uniqueDataIds.Contains(_.trackableId));
            if (newFaces.Any()) {
                return newFaces.Select(_ => {
                    uniqueDataIds.Add(_.trackableId);
                    var indices = _.indices;
                    return new FaceUniqueData {
                        trackableId = TrackableIdSerializable.Create(_.trackableId),
                        indices = indices.IsCreated ? indices.ToArray() : new int[0],
                        uvs = uvsToArray(_.uvs),
                    };
                }).ToArray();
            } else {
                return null;
            }
        
            Vector2Serializable[] uvsToArray(NativeArray<Vector2> array) {
                if (array.IsCreated) {
                    var result = new Vector2Serializable[array.Length];
                    for (int i = 0; i < array.Length; i++) {
                        result[i] = Vector2Serializable.Create(array[i]);
                    }

                    return result;
                } else {
                    return new Vector2Serializable[0];
                }
            }
        }

        ARFaceSerializable[] toSerializable(List<ARFace> argsAdded) {
            return argsAdded.Select(face => ARFaceSerializable.Create(face, getBlendShapeCoefficients(face.trackableId))).ToArray();
        }

        [CanBeNull]
        ARKitBlendShapeCoefficientSerializable[] getBlendShapeCoefficients(TrackableId id) {
            if (!Settings.faceTrackingSettings.sendARKitBlendshapes) {
                return null;
            }
            
            #if (UNITY_IOS || UNITY_EDITOR) && ARFOUNDATION_REMOTE_ENABLE_IOS_BLENDSHAPES
                var arKitFaceSubsystem = manager.subsystem as ARKitFaceSubsystem;
                Assert.IsNotNull(arKitFaceSubsystem);
                using (var coefficients = arKitFaceSubsystem.GetBlendShapeCoefficients(id, Allocator.Temp)) {
                    return coefficients.Select(ARKitBlendShapeCoefficientSerializable.Create).ToArray();
                }
            #else
                return null;
            #endif
        }

        bool shouldSend(ARFacesChangedEventArgs args) {
            if (checkIfTrackingStateUpdated(args)) {
                return true;
            } else {
                return args.added.Any() || args.removed.Any();
            }
        }

        bool checkIfTrackingStateUpdated(ARFacesChangedEventArgs data) {
            var curStates = data.updated.Select(FaceTrackingStateData.Create).ToArray();
            foreach (var cur in curStates) {
                var i = Array.FindIndex(prevStates, _ => _.id == cur.id);
                if (i != -1) {
                    var prev = prevStates[i];
                    var stateChanged = prev.state != cur.state;
                    // we check if pose changed. Android devices receive the same face data under heavy load  
                    if (stateChanged) {
                        log("checkIfTrackingStateUpdated true");
                        prevStates = curStates;
                        return true;
                    }
                }
            }

            prevStates = curStates;
            return false;
        }

        bool checkCurrentFPS() {
            if (Time.time - lastSendTime > 1f / Settings.faceTrackingSettings.maxFPS) {
                lastSendTime = Time.time;
                return true;
            } else {
                return false;
            }
        }

        [Conditional("_")]
        public static void log(string s) {
            Debug.Log(s);
        }
        
        struct FaceTrackingStateData {
            public TrackableId id { get; private set; }
            public TrackingState state { get; private set; }
            public Pose pose { get; private set; }


            public static FaceTrackingStateData Create(ARFace f) {
                return new FaceTrackingStateData {
                    id = f.trackableId,
                    state = f.trackingState,
                    pose = f.transform.LocalPose()
                };
            }
        }

        public override void EditorMessageReceived(EditorToPlayerMessage data) {
            var enableFaceSubsystem = data.enableFaceSubsystem;
            if (enableFaceSubsystem.HasValue) {
                if (!faceTrackingSupportChecked && Defines.isIOS) {
                    faceTrackingSupportChecked = true;
                    DontDestroyOnLoadSingleton.AddCoroutine(checkARKitFaceTrackingPlugin(), nameof(checkARKitFaceTrackingPlugin));
                }
                
                Sender.Instance.SetManagerEnabled(manager, enableFaceSubsystem.Value);
            }
            
            var messageType = data.messageType;
            if (messageType == EditorToPlayerMessageType.Init) {
                manager.facePrefab.GetComponent<ARFaceMeshVisualizer>().enabled = Settings.faceTrackingSettings.showFacesInCompanionApp;
            } else if (messageType.IsStop()) {
                uniqueDataIds.Clear();
            }
        }
    }


    [Serializable]
    public struct FaceUniqueData {
        public TrackableIdSerializable trackableId;
        [NotNull] public int[] indices;
        [NotNull] public Vector2Serializable[] uvs;
    }
    
    
    [Serializable]
    public class FaceSubsystemData {
        [NotNull] public ARFaceSerializable[] added, updated, removed;
        [CanBeNull] public FaceUniqueData[] uniqueData;
        
        
        public FaceSubsystemData(ARFaceSerializable[] addedFaces, ARFaceSerializable[] updatedFaces, ARFaceSerializable[] removedFaces) {
            added = addedFaces;
            updated = updatedFaces;
            removed = removedFaces;
        }
        
        public override string ToString() {
            string result = "";
            if (added.Any()) {
                result += added.Length + " added:\n";
                foreach (var p in added) {
                    result += p.ToString() + "\n";
                }
            }

            if (updated.Any()) {
                result += updated.Length + "updated:\n";
                foreach (var p in updated) {
                    result += p.ToString() + "\n";
                }
            }

            if (removed.Any()) {
                result += removed.Length + "removed:\n";
                foreach (var p in removed) {
                    result += p.ToString() + "\n";
                }
            }

            return result;
        }

        public bool needLogFaces => false;
    }


    [Serializable]
    public struct XRFaceSerializable {
        public TrackableIdSerializable trackableId { get; private set; }
        public PoseSerializable pose { get; private set; }
        public TrackingState trackingState { get; private set; }
        public IntPtr nativePtr { get; private set; }
        public PoseSerializable leftEyePose { get; private set; }
        public PoseSerializable rightEyePose { get; private set; }
        public Vector3Serializable fixationPoint { get; private set; }

        
        public static XRFaceSerializable Create(ARFace f) {
            return new XRFaceSerializable {
                trackableId = TrackableIdSerializable.Create(f.trackableId),
                pose = PoseSerializable.Create(f.transform.LocalPose()),
                trackingState = f.trackingState,
                nativePtr = IntPtr.Zero,
                leftEyePose = PoseSerializable.Create(f.leftEye.LocalPoseOrDefaultIfNull()),
                rightEyePose = PoseSerializable.Create(f.rightEye.LocalPoseOrDefaultIfNull()),
                fixationPoint = Vector3Serializable.Create(f.fixationPoint.LocalPositionOrDefaultIfNull()) 
            };
        }
        
        public XRFace Value => new Union {serializable = new XRFaceSerializableDummy(this)}.nonSerializable;
        
        [StructLayout(LayoutKind.Explicit)]
        struct Union {
            [FieldOffset(0)] public XRFaceSerializableDummy serializable;
            [FieldOffset(0)] public readonly XRFace nonSerializable;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct XRFaceSerializableDummy {
            TrackableId m_TrackableId;
            Pose m_Pose;
            TrackingState m_TrackingState;
            IntPtr m_NativePtr;
            Pose m_LeftEyePose;
            Pose m_RightEyePose;
            Vector3 m_FixationPoint;

            public XRFaceSerializableDummy(XRFaceSerializable f) {
                m_TrackableId = f.trackableId.Value;
                m_Pose = f.pose.Value;
                m_TrackingState = f.trackingState;
                m_NativePtr = f.nativePtr;
                m_LeftEyePose = f.leftEyePose.Value;
                m_RightEyePose = f.rightEyePose.Value;
                m_FixationPoint = f.fixationPoint.Value;
            }
        }
    }
    

    [Serializable]
    public class ARFaceSerializable : ISerializableTrackable<XRFace> {
        XRFaceSerializable xrFaceSerializable;
        public Vector3Serializable[] vertices { get; private set; }
        public Vector3Serializable[] normals { get; private set; }
        [CanBeNull] public ARKitBlendShapeCoefficientSerializable[] blendShapeCoefficients { get; private set; }
        
        public TrackableId trackableId => xrFaceSerializable.trackableId.Value;
        public XRFace Value => xrFaceSerializable.Value;

        public static ARFaceSerializable Create(ARFace f, [CanBeNull] ARKitBlendShapeCoefficientSerializable[] blendShapeCoefficients) {
            return new ARFaceSerializable {
                xrFaceSerializable = XRFaceSerializable.Create(f),
                vertices = toArray(f.vertices, Settings.faceTrackingSettings.sendVertices),
                normals = toArray(f.normals, Settings.faceTrackingSettings.sendNormals),
                blendShapeCoefficients = blendShapeCoefficients
            };
        }

        static Vector3Serializable[] toArray(NativeArray<Vector3> array, bool enable) {
            if (enable && array.IsCreated) {
                var result = new Vector3Serializable[array.Length];
                for (int i = 0; i < array.Length; i++) {
                    result[i] = Vector3Serializable.Create(array[i]);
                }

                return result;
            } else {
                return new Vector3Serializable[0];
            }
        }

        public override string ToString() {
            //return string.Join(",", uvs.Take(100).Select(_ => _.Vector2.ToString()));
            var f = xrFaceSerializable;
            return f.trackableId.ToString() +
                   f.trackingState + ", " +
                   "vertices: " + vertices.Length + ", " + f.pose + ", " +
                   f.nativePtr + ", " + f.leftEyePose + ", " + f.rightEyePose + ", " + f.fixationPoint;
        }
    }
    
    
    [Serializable]
    public struct ARKitBlendShapeCoefficientSerializable {
        int location;
        float coefficient;
    
    #if (UNITY_IOS || UNITY_EDITOR) && ARFOUNDATION_REMOTE_ENABLE_IOS_BLENDSHAPES
        static readonly FieldInfo
            m_BlendShapeLocation = getField("m_BlendShapeLocation"),
            m_Coefficient = getField("m_Coefficient");
    
    
        public static ARKitBlendShapeCoefficientSerializable Create(ARKitBlendShapeCoefficient c) {
            return new ARKitBlendShapeCoefficientSerializable {
                location = (int) c.blendShapeLocation,
                coefficient = c.coefficient
            };
        }

        public ARKitBlendShapeCoefficient Value {
            get {
                object boxed = new ARKitBlendShapeCoefficient();
                m_BlendShapeLocation.SetValue(boxed, (ARKitBlendShapeLocation) location);
                m_Coefficient.SetValue(boxed, coefficient);
                return (ARKitBlendShapeCoefficient) boxed;
            }
        }
    
        [NotNull]
        static FieldInfo getField(string name) {
            var result = typeof(ARKitBlendShapeCoefficient).GetField(name, BindingFlags.Instance | BindingFlags.GetField | BindingFlags.NonPublic);
            Assert.IsNotNull(result);
            return result;
        }
    #endif
    }
}
