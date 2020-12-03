﻿#if UNITY_2019_3_OR_NEWER
    using MeshId = UnityEngine.XR.MeshId;
#else
    using MeshGenerationResult = UnityEngine.Experimental.XR.MeshGenerationResult;
    using MeshId = UnityEngine.Experimental.XR.TrackableId;
    using XRMeshSubsystem = UnityEngine.Experimental.XR.XRMeshSubsystem;
    using MeshInfo = UnityEngine.Experimental.XR.MeshInfo;
    using MeshVertexAttributes = UnityEngine.Experimental.XR.MeshVertexAttributes;
    using MeshChangeState = UnityEngine.Experimental.XR.MeshChangeState;
    using MeshGenerationStatus = UnityEngine.Experimental.XR.MeshGenerationStatus;
    using System.Collections;
#endif
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.XR;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;


namespace ARFoundationRemote.Runtime {
    public partial class MeshSubsystemSender : SubsystemSender {
        [SerializeField] ARSessionOrigin origin = null;
        [SerializeField] MeshFilter meshPrefab = null;

        XRMeshSubsystem realSubsystem;
        ARMeshManager manager;
        readonly Dictionary<MeshId, GenerateMeshAsyncReceiverData> debug_meshGenerationRequests = new Dictionary<MeshId, GenerateMeshAsyncReceiverData>();
        readonly TrackableChangesReceiverBase<MeshInfoSerializable, MeshInfo> receiver = new TrackableChangesReceiverBase<MeshInfoSerializable, MeshInfo>(true);
        readonly HashSet<Mesh> meshes = new HashSet<Mesh>();

        
        void Awake() {
            if (Defines.isARCompanionDefine) {
                XRMeshSubsystemRemote.SetDelegate(this);
                realSubsystem = XRGeneralSettingsRemote.GetRealSubsystem();
                manager = createMeshManager();
            } else {
                throw new Exception("no AR_COMPANION define!");
            }
        }

        /// Create <see cref="ARMeshManager"/> after <see cref="XRMeshSubsystemRemote.SetDelegate"/>
        ARMeshManager createMeshManager() {
            Assert.AreEqual(Vector3.zero, meshPrefab.transform.localPosition, "mesh prefab should be at zero position to display mesh correctly");
            Assert.AreEqual(Vector3.one, meshPrefab.transform.localScale, "meshPrefab.transform.localScale != Vector3.one");
            Assert.AreEqual(origin.transform, transform.parent);
            var meshManager = gameObject.AddComponent<ARMeshManager>();
            meshManager.concurrentQueueSize = int.MaxValue;
            meshManager.meshPrefab = meshPrefab;
            meshManager.enabled = false;
            return meshManager;
        }

        void OnDestroy() {
            CleanMemory();
            XRMeshSubsystemRemote.ClearDelegate(this);
        }

        public override void EditorMessageReceived(EditorToPlayerMessage data) {
            if (realSubsystem == null) {
                log("realSubsystem == null, skipping Editor message");
                if (data.meshingData?.enableARMeshManager == true) {
                    Sender.runningErrorMessage += "- Meshing is not supported on this device\n";
                }
                
                return;
            }

            if (data.messageType == EditorToPlayerMessageType.Init) {
                // this prevents a _bug in AR Foundation related to "Content Placement Offset" object
                var managerTransform = manager.transform;
                managerTransform.SetParent(origin.transform);
                managerTransform.localPosition = Vector3.zero;
                managerTransform.localScale = Vector3.one;
                managerTransform.rotation = Quaternion.identity;
            } else if (data.messageType.IsStop()) {
                log($"{nameof(MeshSubsystemSender)} IsStop()");
                CleanMemory();
                return;
            }
            
            var maybeMeshingData = data.meshingData;
            if (maybeMeshingData.HasValue) {
                var meshingData = maybeMeshingData.Value;
                var enableArMeshManager = meshingData.enableARMeshManager;
                if (enableArMeshManager.HasValue) {
                    log("receive ARMeshManager enabled " + enableArMeshManager.Value);
                    Sender.Instance.SetManagerEnabled(manager, enableArMeshManager.Value);
                }

                var maybeGenerateMeshAsyncRequest = meshingData.generateMeshAsyncRequest;
                if (maybeGenerateMeshAsyncRequest.HasValue) {
                    var editorRequest = maybeGenerateMeshAsyncRequest.Value;
                    log("GenerateMeshAsyncRequest " + editorRequest.meshId);
                    var meshId = editorRequest.meshId.Value;

                    var mesh = new Mesh();
                    meshes.Add(mesh);
                    var attributes = editorRequest.attributes;
                    realSubsystem.GenerateMeshAsync(meshId, mesh, null, attributes, result => {
                        log("onMeshGenerationComplete " + result.MeshId);
                        if (mesh != null) {
                            Assert.IsNotNull(mesh, "IsNotNull(mesh)");

                            bool success = true;
                            if (mesh != result.Mesh) {
                                errorOccursSometimes("realSubsystem.GenerateMeshAsync mesh != result.Mesh");
                                success = false;
                            }

                            if (meshId != result.MeshId) {
                                errorOccursSometimes("realSubsystem.GenerateMeshAsync meshId != result.MeshId");
                                success = false;
                            }

                            var subMeshIndex = 0;
                            var indices = mesh.GetIndices(subMeshIndex);

                            if (indices.All(_ => _ == 0)) {
                                // don't know why, but sometimes indices will have all zero values
                                // this causes mesh flickering
                                success = false;
                            }

                            if (success) {
                                new PlayerToEditorMessage {
                                    meshingData = new MeshingDataPlayer {
                                        generateMeshAsyncResponse = new GenerateMeshAsyncResponse {
                                            meshId = MeshIdSerializable.Create(meshId),
                                            vertices = mesh.vertices.Select(Vector3Serializable.Create).ToArray(),
                                            indices = indices,
                                            normals = attributes.HasFlag(MeshVertexAttributes.Normals) ? mesh.normals.Select(Vector3Serializable.Create).ToArray() : null,
                                            tangents = attributes.HasFlag(MeshVertexAttributes.Tangents) ? mesh.tangents.Select(Vector4Serializable.Create).ToArray() : null,
                                            uvs = attributes.HasFlag(MeshVertexAttributes.UVs) ? mesh.uv.Select(Vector2Serializable.Create).ToArray() : null,
                                            colors = attributes.HasFlag(MeshVertexAttributes.Colors) ? mesh.colors.Select(ColorSerializable.Create).ToArray() : null,
                                            #if (UNITY_IOS || UNITY_EDITOR) && ARKIT_INSTALLED && ARFOUNDATION_4_0_OR_NEWER
                                            faceClassifications = tryGetFaceClassifications(meshId.trackableId()),
                                            #endif
                                            Status = result.Status
                                        }
                                    }
                                }.Send();
                            } else {
                                new PlayerToEditorMessage {
                                    meshingData = new MeshingDataPlayer {
                                        generateMeshAsyncResponse = new GenerateMeshAsyncResponse {
                                            meshId = MeshIdSerializable.Create(meshId),
                                            Status = MeshGenerationStatus.UnknownError
                                        }
                                    }
                                }.Send();
                            }

                            // just to visualize. There is no guarantee that companion app is showing the correct results
                            if (Settings.Instance.arCompanionSettings.meshingSettings.showMeshesInCompanionApp && debug_meshGenerationRequests.TryGetValue(meshId, out var request)) {
                                var senderMesh = request.mesh;
                                if (senderMesh != null) {
                                    senderMesh.CopyFrom(mesh, subMeshIndex);
                                }

                                request.onMeshGenerationComplete(result);
                            }
                        } else {
                            log($"mesh was destroyed {meshId}");
                        }

                        var removed = meshes.Remove(mesh);
                        // Assert.IsTrue(removed); // can be false if CleanMemory() was called before callback 
                        Object.Destroy(mesh);
                    });
                }

                var density = meshingData.meshDensity;
                if (density.HasValue) {
                    meshDensity = density.Value;
                }

                var boundingVolumeData = meshingData.boundingVolumeData;
                if (boundingVolumeData.HasValue) {
                    var managerTransform = manager.transform;
                    var volumeData = boundingVolumeData.Value;
                    managerTransform.localPosition = volumeData.origin.Value;
                    managerTransform.localScale = volumeData.extents.Value;
                }

                #if (UNITY_IOS || UNITY_EDITOR) && ARKIT_INSTALLED && ARFOUNDATION_4_0_OR_NEWER
                var setClassificationEnabled = meshingData.setClassificationEnabled;
                if (setClassificationEnabled.HasValue) {
                    log("ARKitMeshSubsystemExtensions.SetClassificationEnabled " + setClassificationEnabled.Value);
                    UnityEngine.XR.ARKit.ARKitMeshSubsystemExtensions.SetClassificationEnabled(realSubsystem, setClassificationEnabled.Value);
                    Assert.AreEqual(true, isClassificationEnabled);
                }

                var getClassificationEnabled = meshingData.getClassificationEnabled;
                if (getClassificationEnabled.HasValue) {
                    new PlayerToEditorMessage {
                        meshingData = new MeshingDataPlayer {
                            classificationEnabled = isClassificationEnabled
                        },
                        responseGuid = data.requestGuid
                    }.Send();
                }
                #endif
            }
        }
        
        void CleanMemory() {
            foreach (var _ in meshes) {
                Assert.IsNotNull(_);
                Object.Destroy(_);
            }
            meshes.Clear();
            
            manager.DestroyAllMeshes();
            debug_meshGenerationRequests.Clear();
            receiver.Reset();
        }

        #if (UNITY_IOS || UNITY_EDITOR) && ARKIT_INSTALLED && ARFOUNDATION_4_0_OR_NEWER
            [CanBeNull]
            UnityEngine.XR.ARKit.ARMeshClassification[] tryGetFaceClassifications(TrackableId meshId) {
                if (isClassificationEnabled) {
                    using (var result = UnityEngine.XR.ARKit.ARKitMeshSubsystemExtensions.GetFaceClassifications(realSubsystem, meshId, Unity.Collections.Allocator.Temp)) {
                        return result.ToArray();
                    }
                } else {
                    return null;
                }
            }
            
            bool isClassificationEnabled => UnityEngine.XR.ARKit.ARKitMeshSubsystemExtensions.GetClassificationEnabled(realSubsystem);
        #endif

   
        [Conditional("_")]
        public static void log(string s) {
            Debug.Log("MeshSubsystem: " + s);
        }
        
        [Conditional("_")]
        public static void errorOccursSometimes(string s) {
            Debug.Log("Mesh sender error: " + s);
        }
    }

    public partial class MeshSubsystemSender : IXRMeshSubsystem {        
        /// <summary>
        /// Explicit interface implementation doesn't receive Unity event callback.
        /// This means we can safely use Start method name here.
        /// </summary>
        void ISubsystem.Start() {
            realSubsystem.Start();
        }

        void ISubsystem.Stop() {
            realSubsystem.Stop();
        }

        void ISubsystem.Destroy() {
            realSubsystem.Destroy();
        }

        bool ISubsystem.running => realSubsystem.running;
        
        /// <summary>
        /// after ARMeshManager.DestroyAllMeshes(), previously added meshes will be reported as updated
        /// we use TrackableChangesReceiverBase to turn updated meshes back into added
        /// </summary>
        bool IXRMeshSubsystem.TryGetMeshInfos(List<MeshInfo> meshInfosOut) {
            meshInfosOut.Clear();
            var infos = new List<MeshInfo>();
            if (realSubsystem.TryGetMeshInfos(infos)) {
                var modified = new List<MeshInfoSerializable>();
                foreach (var meshInfo in infos) {
                    switch (meshInfo.ChangeState) {
                        case MeshChangeState.Added:
                            receiver.Receive(new[] {MeshInfoSerializable.Create(meshInfo)}, new MeshInfoSerializable[0], new MeshInfoSerializable[0]);
                            break;
                        case MeshChangeState.Updated:
                            receiver.Receive(new MeshInfoSerializable[0], new[] {MeshInfoSerializable.Create(meshInfo)}, new MeshInfoSerializable[0]);
                            break;
                        case MeshChangeState.Removed:
                            receiver.Receive(new MeshInfoSerializable[0], new MeshInfoSerializable[0], new[] {MeshInfoSerializable.Create(meshInfo)});
                            break;
                        case MeshChangeState.Unchanged:
                            modified.Add(MeshInfoSerializable.Create(meshInfo));
                            break;
                    }
                }
                
                foreach (var _ in receiver.updated.Values) {
                    Assert.AreEqual(MeshChangeState.Updated, _.ChangeState, "AreEqual(MeshChangeState.Updated, _.ChangeState)");
                }
                
                foreach (var _ in receiver.removed.Values) {
                    Assert.AreEqual(MeshChangeState.Removed, _.ChangeState, "AreEqual(MeshChangeState.Removed, _.ChangeState)");
                }
                
                modified.AddRange(
                    receiver.added.Values.Select(_ => _.WithState(MeshChangeState.Added))
                    .Concat(receiver.updated.Values)
                    .Concat(receiver.removed.Values));
                receiver.OnAfterGetChanges();
                
                meshInfosOut.AddRange(modified.Select(_ => _.Value));
                
                log("send meshInfos " + modified.Count);
                new PlayerToEditorMessage {
                    meshingData = new MeshingDataPlayer {
                        meshInfos = modified        
                    }
                }.Send();
                
                return true;
            } else {
                return false;
            }
        }
        
        void IXRMeshSubsystem.GenerateMeshAsync(MeshId meshId, Mesh mesh, MeshCollider meshCollider, MeshVertexAttributes attributes, Action<MeshGenerationResult> onMeshGenerationComplete) {
            log("meshGenerationRequests.Add " + meshId);
            debug_meshGenerationRequests[meshId] = new GenerateMeshAsyncReceiverData {
                mesh = mesh,
                meshCollider = meshCollider,
                onMeshGenerationComplete = onMeshGenerationComplete
            };
        }

        public float meshDensity {
            get => throw new NotSupportedException();
            set {
            }
        }

        bool IXRMeshSubsystem.SetBoundingVolume(Vector3 origin, Vector3 extents) {
            return false;
        }
    }

    
    [Serializable]
    public struct MeshingDataPlayer {
        [CanBeNull] public List<MeshInfoSerializable> meshInfos;
        public GenerateMeshAsyncResponse? generateMeshAsyncResponse;
        public bool? classificationEnabled;
    }


    [Serializable]
    public struct MeshingDataEditor {
        public bool? enableARMeshManager;
        public GenerateMeshAsyncRequest? generateMeshAsyncRequest;
        public float? meshDensity;
        public SetBoundingVolumeCallData? boundingVolumeData;
        public bool? setClassificationEnabled;
        public bool? getClassificationEnabled; // bool value in not used
    }

    
    [Serializable]
    public struct SetBoundingVolumeCallData {
        public Vector3Serializable origin;
        public Vector3Serializable extents;
    }
    
    
    [Serializable]
    public struct GenerateMeshAsyncRequest {
        public MeshIdSerializable meshId;
        public MeshVertexAttributes attributes;
    }


    [Serializable]
    public struct GenerateMeshAsyncResponse {
        public MeshIdSerializable meshId;
        public Vector3Serializable[] vertices;
        public int[] indices;
        [CanBeNull] public Vector3Serializable[] normals;
        [CanBeNull] public Vector4Serializable[] tangents;
        [CanBeNull] public Vector2Serializable[] uvs;
        [CanBeNull] public ColorSerializable[] colors;
        public MeshGenerationStatus Status;
        #if (UNITY_IOS || UNITY_EDITOR) && ARKIT_INSTALLED && ARFOUNDATION_4_0_OR_NEWER
            [CanBeNull] public UnityEngine.XR.ARKit.ARMeshClassification[] faceClassifications;
        #endif
    }
    

    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct MeshInfoSerializable : ISerializableTrackable<MeshInfo> {
        MeshIdSerializable MeshId;
        public MeshChangeState ChangeState;
        int PriorityHint;

        public static MeshInfoSerializable Create(MeshInfo info) => new Union {nonSerializable = info}.serializable;

        public TrackableId trackableId => MeshId.Value.trackableId();
        
        public MeshInfo Value => new Union {serializable = this}.nonSerializable;

        public MeshInfoSerializable WithState(MeshChangeState state) {
            return new MeshInfoSerializable {
                MeshId = MeshId,
                ChangeState = state,
                PriorityHint = PriorityHint
            };
        }

        [StructLayout(LayoutKind.Explicit)]
        struct Union {
            [FieldOffset(0)] public MeshInfoSerializable serializable;
            [FieldOffset(0)] public MeshInfo nonSerializable;
        }
    }
    
    

    [StructLayout(LayoutKind.Sequential)]
    public struct MeshGenerationResultWrapper {
        public MeshId MeshId;
        public Mesh Mesh;
        public MeshCollider MeshCollider;
        public MeshGenerationStatus Status;
        public MeshVertexAttributes Attributes;
        
        public MeshGenerationResult Value => new Union {serializable = this}.nonSerializable;
        
        [StructLayout(LayoutKind.Explicit)]
        struct Union {
            [FieldOffset(0)] public MeshGenerationResultWrapper serializable;
            [FieldOffset(0)] public readonly MeshGenerationResult nonSerializable;
        }
    }
    
    
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct MeshIdSerializable {
        ulong m_SubId1, m_SubId2;


        public static MeshIdSerializable Create(MeshId id) {
            return new union {nonSerializable = id}.serializable;
        }

        public MeshId Value => new union {serializable = this}.nonSerializable;
            
        [StructLayout(LayoutKind.Explicit)]
        struct union {
            // MeshId LayoutKind is not Sequential. This may cause problems
            [FieldOffset(0)] public MeshIdSerializable serializable;
            [FieldOffset(0)] public MeshId nonSerializable;
        }
    }


    public class GenerateMeshAsyncReceiverData {
        public Mesh mesh;
        [CanBeNull] public MeshCollider meshCollider;
        public Action<MeshGenerationResult> onMeshGenerationComplete;
        public MeshVertexAttributes attributes;
    }
    
            
    static class MeshExtensions {
        public static void CopyFrom(this Mesh mesh, Mesh other, int submeshIndex) {
            mesh.Clear();
            mesh.SetVerticesCustom(other.vertices);
            mesh.SetIndices(other.GetIndices(submeshIndex), other.GetTopology(submeshIndex), submeshIndex);
            mesh.SetNormalsCustom(other.normals);
            mesh.SetTangentsCustom(other.tangents);
            mesh.SetUVsCustom(0, other.uv);
            mesh.SetColorsCustom(other.colors);
            mesh.RecalculateBounds();
        }

        static void SetVerticesCustom(this Mesh m, Vector3[] array) {
            #if UNITY_2019_2
                m.SetVertices(array.ToList());
            #else
                m.SetVertices(array);
            #endif
        }

        static void SetNormalsCustom(this Mesh m, Vector3[] array) {
            #if UNITY_2019_2
                m.SetNormals(array.ToList());
            #else
                m.SetNormals(array);
            #endif
        }

        static void SetTangentsCustom(this Mesh m, Vector4[] array) {
            #if UNITY_2019_2
                m.SetTangents(array.ToList());
            #else
                m.SetTangents(array);
            #endif
        }

        static void SetUVsCustom(this Mesh m, int channel, Vector2[] array) {
            #if UNITY_2019_2
                m.SetUVs(channel, array.ToList());
            #else
                m.SetUVs(channel, array);
            #endif
        }

        static void SetColorsCustom(this Mesh m, Color[] array) {
            #if UNITY_2019_2
                m.SetColors(array.ToList());
            #else
                m.SetColors(array);
            #endif
        }
    }
}
