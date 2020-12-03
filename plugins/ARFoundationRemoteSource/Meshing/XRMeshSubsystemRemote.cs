#if UNITY_2019_3_OR_NEWER
    using MeshId = UnityEngine.XR.MeshId;
#else
    using MeshGenerationResult = UnityEngine.Experimental.XR.MeshGenerationResult;
    using MeshId = UnityEngine.Experimental.XR.TrackableId;
    using XRMeshSubsystem = UnityEngine.Experimental.XR.XRMeshSubsystem;
    using XRMeshSubsystemDescriptor = UnityEngine.Experimental.XR.XRMeshSubsystemDescriptor;
    using MeshInfo = UnityEngine.Experimental.XR.MeshInfo;
    using MeshVertexAttributes = UnityEngine.Experimental.XR.MeshVertexAttributes;
#endif
using System;
using System.Collections.Generic;
using System.Diagnostics;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.XR;
using UnityEngine.XR.Management;
using Debug = UnityEngine.Debug;


namespace ARFoundationRemote.Runtime {
    public interface IXRMeshSubsystem : ISubsystem {
        /// clears infos after the first call so the second subsequent call in the same frame will always return empty List
        bool TryGetMeshInfos(List<MeshInfo> meshInfosOut);
        void GenerateMeshAsync(
            MeshId meshId,
            [NotNull] Mesh mesh,
            [CanBeNull] MeshCollider meshCollider,
            MeshVertexAttributes attributes,
            [NotNull] Action<MeshGenerationResult> onMeshGenerationComplete);
        float meshDensity { get; set; }
        bool SetBoundingVolume(Vector3 origin, Vector3 extents);
    }

    public class XRMeshSubsystemRemote : IXRMeshSubsystem {
        static IXRMeshSubsystem subsystemDelegate;

        
        public static void SetDelegate([NotNull] IXRMeshSubsystem del) {
            log("SetDelegate");
            Assert.IsNull(subsystemDelegate);
            subsystemDelegate = del;
        }

        public static void ClearDelegate([NotNull] IXRMeshSubsystem del) {
            log("ClearDelegate");
            Assert.AreEqual(subsystemDelegate, del);
            subsystemDelegate = null;
        }

        public bool TryGetMeshInfos(List<MeshInfo> meshInfosOut) {
            return subsystemDelegate.TryGetMeshInfos(meshInfosOut);
        }

        public void GenerateMeshAsync(
            MeshId meshId,
            Mesh mesh,
            MeshCollider meshCollider,
            MeshVertexAttributes attributes,
            Action<MeshGenerationResult> onMeshGenerationComplete) {
            subsystemDelegate.GenerateMeshAsync(meshId, mesh, meshCollider, attributes, onMeshGenerationComplete);
        }

        public float meshDensity {
            get => subsystemDelegate.meshDensity;
            set => subsystemDelegate.meshDensity = value;
        }

        public bool SetBoundingVolume(Vector3 origin, Vector3 extents) {
            return subsystemDelegate.SetBoundingVolume(origin, extents);
        }

        void ISubsystem.Start() {
            subsystemDelegate.Start();
        }

        public void Stop() {
            subsystemDelegate.Stop();
        }

        public void Destroy() {
            subsystemDelegate.Destroy();
        }

        public bool running => subsystemDelegate.running;
        
        [Conditional("_")]
        static void log(string s) {
            Debug.Log(s);
        }
    }


    public class XRGeneralSettingsRemote {
        public static readonly XRGeneralSettingsRemote Instance = new XRGeneralSettingsRemote();

        public class _Manager {
            public readonly _Loader activeLoader = new _Loader();

            public class _Loader {
                [NotNull] 
                static readonly IXRMeshSubsystem subsystem = new XRMeshSubsystemRemote();

                [CanBeNull]
                public T GetLoadedSubsystem<T>() where T : class, IXRMeshSubsystem {
                    if (Application.isEditor) {
                        var result = subsystem as T;
                        Assert.IsNotNull(result);
                        return result;
                    } else {
                        Assert.IsTrue(isARCompanionDefine);
                        if (GetRealSubsystem() != null) {
                            var result = subsystem as T;
                            Assert.IsNotNull(result);
                            return result;
                        } else {
                            return null;
                        }
                    }
                }

                static bool isARCompanionDefine {
                    get {
                        #if AR_COMPANION
                            return true;
                        #else
                            return false;
                        #endif
                    }
                }
            }
        }

        public readonly _Manager Manager = new _Manager();
        
        [CanBeNull]
        public static XRMeshSubsystem GetRealSubsystem()
        {
            XRMeshSubsystem activeSubsystem = null;

            // Query the currently active loader for the created subsystem, if one exists.
            if (XRGeneralSettings.Instance != null && XRGeneralSettings.Instance.Manager != null)
            {
                var loader = XRGeneralSettings.Instance.Manager.activeLoader;
                if (loader != null)
                {
                    activeSubsystem = loader.GetLoadedSubsystem<XRMeshSubsystem>();
                }
            }

            if (activeSubsystem == null)
            {
                // Debug.LogWarning($"No active {typeof(XRMeshSubsystem).FullName} is available. Please ensure that a valid loader configuration exists in the XR project settings and that meshing is supported.");
            }

            return activeSubsystem;
        }
    }

    public static class SubsystemManagerRemote {
        public static void GetSubsystemDescriptors(List<XRMeshSubsystemDescriptorRemote> sSubsystemDescriptors) {
            sSubsystemDescriptors.Clear();
            sSubsystemDescriptors.Add(new XRMeshSubsystemDescriptorRemote());
        }
    }

    public class XRMeshSubsystemDescriptorRemote {
        public string id { get; } = nameof(XRMeshSubsystemDescriptorRemote);

        public IXRMeshSubsystem Create() {
            return XRGeneralSettingsRemote.Instance.Manager.activeLoader.GetLoadedSubsystem<IXRMeshSubsystem>();
        }
    }
}
