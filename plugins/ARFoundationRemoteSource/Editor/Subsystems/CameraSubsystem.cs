using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ARFoundationRemote.Runtime;
using JetBrains.Annotations;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;
using UnityEngine.XR.ARSubsystems;
using Debug = UnityEngine.Debug;


namespace ARFoundationRemote.Editor {
    public partial class CameraSubsystem : XRCameraSubsystem {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void RegisterDescriptor() {
            var thisType = typeof(CameraSubsystem);
            bool isARKit = EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS;
            Register(new XRCameraSubsystemCinfo {
                id = thisType.Name,
                #if UNITY_2020_2_OR_NEWER
                    providerType = typeof(CameraSubsystemProvider),
                    subsystemTypeOverride = thisType,
                #else
                    implementationType = thisType,
                #endif
                supportsAverageBrightness = !isARKit, 
                supportsAverageColorTemperature = isARKit,
                supportsColorCorrection = !isARKit,
                supportsDisplayMatrix = true,
                supportsProjectionMatrix = true,
                supportsTimestamp = true,
                supportsCameraConfigurations = true,
                supportsCameraImage = false, // CPU camera image is not supported
                supportsAverageIntensityInLumens = isARKit,
                supportsFocusModes = false
            });
        }

        #if !UNITY_2020_2_OR_NEWER
        protected override Provider CreateProvider() {
            return new CameraSubsystemProvider();
        }
        #endif

        class CameraSubsystemProvider : Provider {
            public CameraSubsystemProvider() {
                cameraMaterial = CreateCameraMaterial(getShaderName());
            }

            [CanBeNull]
            static string getShaderName() {
                #if UNITY_IOS && ARKIT_INSTALLED
                    return Application.platform == RuntimePlatform.WindowsEditor ? "Unlit/ARKitBackgroundWindows" : UnityEngine.XR.ARKit.ARKitCameraSubsystem.backgroundShaderName;
                #endif
                
                #if UNITY_ANDROID && ARCORE_INSTALLED
                    return "Unlit/ARCoreBackgroundEditor";
                #endif

                #pragma warning disable 162
                Debug.LogWarning($"{Constants.packageName}: {EditorUserBuildSettings.activeBuildTarget} doesn't support camera video. Please ensure that you selected a correct build target and installed XR plugin.");
                return null;
                #pragma warning restore 162
            }

            #if ARFOUNDATION_4_0_OR_NEWER
                Feature currentRequestedCamera = Feature.None;

                public override Feature requestedCamera {
                    get => currentRequestedCamera;
                    set {
                        if (currentRequestedCamera != value) {
                            currentRequestedCamera = value;
                            new EditorToPlayerMessage {requestedCamera = value}.Send();
                            log("send currentRequestedCamera " + value);
                        }
                    }
                }

                Feature? _requestedLightEstimation;

                public override Feature requestedLightEstimation {
                    get => _requestedLightEstimation ?? Feature.None;
                    set {
                        if (_requestedLightEstimation != value) {
                            _requestedLightEstimation = value;                            
                            new EditorToPlayerMessage{requestedLightEstimation = (int) value}.Send();
                            log($"send requestedLightEstimation {value}");
                        }
                    }
                }
            #else
                LightEstimationMode? _lightEstimationMode;
                
                public override bool TrySetLightEstimationMode(LightEstimationMode lightEstimationMode) {
                    if (_lightEstimationMode != lightEstimationMode) {
                        _lightEstimationMode = lightEstimationMode;
                        new EditorToPlayerMessage {requestedLightEstimation = (int) lightEstimationMode}.Send();
                    }

                    // correct return value is not supported 
                    return true;
                }
            #endif

            [CanBeNull] public override Material cameraMaterial { get; }

            public override bool permissionGranted => true;

            public override bool TryGetFrame(XRCameraParams cameraParams, out XRCameraFrame cameraFrame) {
                if (receivedCameraFrame.HasValue) {
                    cameraFrame = receivedCameraFrame.Value.frame.Value;
                    return true;
                } else {
                    cameraFrame = default;
                    return false;    
                }
            }

            bool enabled = true;

            public override NativeArray<XRTextureDescriptor> GetTextureDescriptors(XRTextureDescriptor defaultDescriptor, Allocator allocator) {
                if (!Settings.EnableBackgroundVideo) {
                    return new NativeArray<XRTextureDescriptor>(0, allocator);
                }
                
                if (enabled) {
                    if (receivedCameraFrame.HasValue && textures != null && cameraMaterial != null) {
                        var propertyNames = receivedCameraFrame.Value.textures.Select(_ => _.propName);
                        if (propertyNames.Any(_ => !SupportCheck.CheckCameraAndOcclusionSupport(cameraMaterial, _))) {
                            enabled = false;
                            return new NativeArray<XRTextureDescriptor>(0, allocator);
                        }

                        var result = textures.Select(_ => _.descriptor).ToArray();
                        return new NativeArray<XRTextureDescriptor>(result, allocator);
                    }
                }
                
                return new NativeArray<XRTextureDescriptor>(0, allocator);
            }

            public override bool invertCulling => receivedCameraFrame?.invertCulling ?? false;

            #if AR_SUBSYSTEMS_3_1_3
            public override void GetMaterialKeywords(out List<string> enabledKeywords, out List<string> disabledKeywords) {
                if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS) {
                    const string urp = "ARKIT_BACKGROUND_URP";
                    const string lwrp = "ARKIT_BACKGROUND_LWRP";
                    var urpKeywords = new List<string> {urp};
                    var lwrpKeywords = new List<string> {lwrp};
                    if (GraphicsSettings.renderPipelineAsset == null) {
                        enabledKeywords = null;
                        disabledKeywords = new List<string> {urp, lwrp};
                    } else if (isURP()) {
                        enabledKeywords = urpKeywords;
                        disabledKeywords = lwrpKeywords;
                    } else if (isLWRP()) {
                        enabledKeywords = lwrpKeywords;
                        disabledKeywords = urpKeywords;
                    }  else {
                        enabledKeywords = null;
                        disabledKeywords = null;
                    }
                } else {
                    enabledKeywords = null;
                    disabledKeywords = null;
                }
            }
            #endif

            static bool isURP() {
                #if MODULE_URP_ENABLED
                    return GraphicsSettings.renderPipelineAsset is UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
                #else
                    return false;
                #endif
            }
            
            /// LWRP was renamed into URP starting from Unity 2019.3. Even official ARKit XR Plugin doesn't compile with LWRP 
            static bool isLWRP() {
                #if MODULE_LWRP_ENABLED
                    return GraphicsSettings.renderPipelineAsset is UnityEngine.Rendering.LWRP.LightweightRenderPipelineAsset;
                #else
                    return false;
                #endif
            }

            public override NativeArray<XRCameraConfiguration> GetConfigurations(XRCameraConfiguration defaultCameraConfiguration, Allocator allocator) {
                var configurationsContainer = blockUntilReceive(new CameraDataEditor {
                    request = CameraDataEditorRequest.GetAllConfigurations
                }).allConfigurations;
                Assert.IsTrue(configurationsContainer.HasValue);
                var configurations = configurationsContainer.Value;
                if (configurations.isSupported) {
                    return new NativeArray<XRCameraConfiguration>(configurations.configs.Select(_ => _.Value).ToArray(), allocator);
                } else {
                    throw new Exception($"{Constants.packageName}: your AR device doesn't support camera configurations");
                }
            }

            public override XRCameraConfiguration? currentConfiguration {
                get =>
                    blockUntilReceive(new CameraDataEditor {
                        request = CameraDataEditorRequest.GetCurrentConfiguration
                    }).currentConfiguration?.Value;
                set {
                    var error = blockUntilReceive(new CameraDataEditor {
                        request = CameraDataEditorRequest.SetCurrentConfiguration,
                        configToSet = CameraConfigurationSerializable.Create(value)
                    }).error;
                    
                    if (error != null) {
                        throw new Exception(error);
                    }
                }
            }

            static CameraData blockUntilReceive(CameraDataEditor cameraDataEditor) {
                var result = Connection.receiverConnection.BlockUntilReceive(new EditorToPlayerMessage {
                    cameraData = cameraDataEditor
                }).cameraData;
                Assert.IsTrue(result.HasValue);
                return result.Value;
            }
        
            public override void Destroy() {
                if (textures != null) {
                    foreach (var _ in textures) {
                        _.OnDestroy();
                    }
                }
            }
        
            [Conditional("_")]
            static void log(string msg) {
                Debug.Log($"{nameof(CameraSubsystem)}: {msg}");
            }
        }
    }
}
