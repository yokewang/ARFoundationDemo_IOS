#if ARFOUNDATION_4_0_OR_NEWER
    using ARFoundationRemote.RuntimeEditor;
#endif
using System.Collections.Generic;
using System.Linq;
using ARFoundationRemote.Runtime;
using UnityEngine;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;


namespace ARFoundationRemote.Editor {
    public class ARFoundationRemoteLoader: XRLoaderHelper {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void initOnLoad() {
            bool isPluginEnabled() {
                var xrGeneralSettings = XRGeneralSettings.Instance;
                if (xrGeneralSettings != null) {
                    var xrManagerSettings = xrGeneralSettings.Manager;
                    if (xrManagerSettings != null) {
                        var loaders = xrManagerSettings.loaders;
                        // Debug.Log($"active loader: {xrManagerSettings.activeLoader}", xrManagerSettings.activeLoader);
                        // Debug.Log($"loaders: {string.Join(", ", loaders)}");
                        if (loaders != null) {
                            return loaders.OfType<ARFoundationRemoteLoader>().Any();
                        }
                    }
                }

                return false;
            }

            if (!isPluginEnabled() && Settings.Instance.logStartupErrors) {
                Debug.LogError("Please enable \"" + Constants.packageName + "\" provider in Project Settings -> XR Plug-in Management -> PC, Mac & Linux Standalone");
            }
        }
        
        public override bool Initialize() {
            #if ARFOUNDATION_4_0_OR_NEWER
                CreateSubsystem<XRObjectTrackingSubsystemDescriptor, XRObjectTrackingSubsystem>(new List<XRObjectTrackingSubsystemDescriptor>(), nameof(ObjectTrackingSubsystem));
                CreateSubsystem<XRHumanBodySubsystemDescriptor, XRHumanBodySubsystem>(new List<XRHumanBodySubsystemDescriptor>(), nameof(HumanBodySubsystem));
                // create OcclusionSubsystem before XRSessionSubsystem because XRSessionSubsystem spawn Receiver and Receiver needs OcclusionSubsystem  
                CreateSubsystem<XROcclusionSubsystemDescriptor, XROcclusionSubsystem>(new List<XROcclusionSubsystemDescriptor>(), nameof(OcclusionSubsystem));
            #endif
            CreateSubsystem<XRSessionSubsystemDescriptor, XRSessionSubsystem>(new List<XRSessionSubsystemDescriptor>(), typeof(SessionSubsystem).Name);
            CreateSubsystem<XRPlaneSubsystemDescriptor, XRPlaneSubsystem>(new List<XRPlaneSubsystemDescriptor>(), typeof(PlaneSubsystem).Name);
            CreateSubsystem<XRDepthSubsystemDescriptor, XRDepthSubsystem>(new List<XRDepthSubsystemDescriptor>(), typeof(DepthSubsystem).Name);
            CreateSubsystem<XRFaceSubsystemDescriptor, XRFaceSubsystem>(new List<XRFaceSubsystemDescriptor>(), typeof(FaceSubsystem).Name);
            CreateSubsystem<XRCameraSubsystemDescriptor, XRCameraSubsystem>(new List<XRCameraSubsystemDescriptor>(), typeof(CameraSubsystem).Name);
            CreateSubsystem<XRImageTrackingSubsystemDescriptor, XRImageTrackingSubsystem>(new List<XRImageTrackingSubsystemDescriptor>(), typeof(ImageTrackingSubsystem).Name);
            CreateSubsystem<XRRaycastSubsystemDescriptor, XRRaycastSubsystem>(new List<XRRaycastSubsystemDescriptor>(), typeof(RaycastSubsystem).Name);
            CreateSubsystem<XRAnchorSubsystemDescriptor, XRAnchorSubsystem>(new List<XRAnchorSubsystemDescriptor>(), nameof(AnchorSubsystem));
            return true;
        }

        public override bool Deinitialize() {
            DestroySubsystem<XRSessionSubsystem>();
            DestroySubsystem<XRPlaneSubsystem>();
            DestroySubsystem<XRDepthSubsystem>();
            DestroySubsystem<XRFaceSubsystem>();
            DestroySubsystem<XRCameraSubsystem>();
            DestroySubsystem<XRImageTrackingSubsystem>();
            DestroySubsystem<XRRaycastSubsystem>();
            DestroySubsystem<XRAnchorSubsystem>();
            #if ARFOUNDATION_4_0_OR_NEWER
                DestroySubsystem<XROcclusionSubsystem>();
                DestroySubsystem<XRHumanBodySubsystem>();
                DestroySubsystem<XRObjectTrackingSubsystem>();
            #endif
            return true;
        }
    }
}
