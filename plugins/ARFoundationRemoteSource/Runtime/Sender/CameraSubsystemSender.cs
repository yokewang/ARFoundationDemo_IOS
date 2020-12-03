using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using JetBrains.Annotations;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Debug = UnityEngine.Debug;


namespace ARFoundationRemote.Runtime {
    public class CameraSubsystemSender : ISubsystemSender {
        public static CameraSubsystemSender Instance { get; private set; }
        public ARCameraManager cameraManager { get; }

        float lastSendTime;
        Dictionary<int, string> propIdsAndNamesDict;
        bool needResetConfig;


        public CameraSubsystemSender(ARCameraManager manager) {
            Instance = this;
            cameraManager = manager;
            manager.frameReceived += frameReceived;
        }

        void frameReceived(ARCameraFrameEventArgs args) {
            if (ARSession.state < ARSessionState.SessionInitializing) {
                return;
            }

            {
                // this causes crash on Android. We should not store the XRCameraConfiguration
                // cameraManager.currentConfiguration = iniConfig;

                // instead, I set the first config
                // can't reliably set first config here because Editor will still report old config on session start
                // tryResetConfig();
            }
            
            if (shouldSend()) {
                var payload = ARCameraFrameEventArgsSerializable.Create(args);
                //Debug.Log("send frame args" + args.ToString());
                //Debug.Log("serialized" + payload.frame.Value.ToString());
                new PlayerToEditorMessage {
                    cameraData = new CameraData {
                        cameraFrame = payload
                    }
                }.Send();
            }
        }

        bool shouldSend() {
            if (Time.time - lastSendTime > 1f / Settings.cameraVideoSettings.maxFPS && Connection.senderConnection.CanSendNonCriticalMessage) {
                lastSendTime = Time.time;
                return true;
            } else {
                return false;
            }
        }

        public string PropIdToName(int id) {
            var found = PropIdToName(id, out var result);
            Assert.IsTrue(found, "PropIdToName not found id {id");
            return result;
        }

        public bool PropIdToName(int id, out string propName) {
            cachePropNamesIfNeeded();
            var result = propIdsAndNamesDict.TryGetValue(id, out propName);
            /*if (!result) {
                DebugUtils.LogErrorOnce($"Texture property name not found, id: {id}. This message is harmless if you use preview version of AR Foundation.");
            }*/

            return result;
        }

        void cachePropNamesIfNeeded() {
            if (propIdsAndNamesDict == null) {
                propIdsAndNamesDict = new Dictionary<int, string>();
                foreach (var propName in cameraManager.subsystem.cameraMaterial.GetTexturePropertyNames()) {
                    var id = Shader.PropertyToID(propName);
                    propIdsAndNamesDict[id] = propName;
                }
            }
        }

        void ISubsystemSender.EditorMessageReceived(EditorToPlayerMessage data) {
            switch (data.messageType) {
                case EditorToPlayerMessageType.ResumeSession:
                    // can't set initial config here because GetConfigurations returns no configs before first frameReceived event
                    break;
                case EditorToPlayerMessageType.StopSessionAndTryReloadScene:
                    // can't set initial config here because GetConfigurations returns no configs while session is disabled
                    needResetConfig = true;
                    return;
            }
            
            #if ARFOUNDATION_4_0_OR_NEWER
                var requestedCamera = data.requestedCamera;
                if (requestedCamera.HasValue) {
                    var requestedFacingDirection = (CameraFacingDirection) requestedCamera.Value;
                    Sender.logSceneSpecific("received requestedFacingDirection " + requestedFacingDirection);
                    cameraManager.requestedFacingDirection = requestedFacingDirection;
                }
            #endif
            
            var requestedLightEstimation = data.requestedLightEstimation;
            if (requestedLightEstimation.HasValue) {
                #if ARFOUNDATION_4_0_OR_NEWER
                    var value = ((Feature) requestedLightEstimation.Value).ToLightEstimation();
                    Sender.logSceneSpecific($"receive requestedLightEstimation {value}");
                    cameraManager.requestedLightEstimation = value;
                #else
                    var value = (LightEstimationMode) requestedLightEstimation.Value;
                    Sender.logSceneSpecific($"receive requestedLightEstimation {value}");
                    cameraManager.lightEstimationMode = value;
                #endif
            }

            var request = data.cameraData?.request;
            if (request.HasValue) {
                void send(CameraData cameraData) {
                    var requestGuid = data.requestGuid;
                    Assert.IsTrue(requestGuid.HasValue);
                    new PlayerToEditorMessage {
                        cameraData = cameraData,
                        responseGuid = requestGuid.Value
                    }.Send();
                }
                
                switch (request.Value) {
                    case CameraDataEditorRequest.GetAllConfigurations:
                        send(new CameraData {
                            allConfigurations = serializeConfigurations()
                        });    
                        break;
                    case CameraDataEditorRequest.GetCurrentConfiguration:
                        send(new CameraData {
                            currentConfiguration = serializeCurrentConfiguration()
                        });
                        break;
                    case CameraDataEditorRequest.SetCurrentConfiguration:
                        waitForAsyncConversionsToFinish(() => {
                            string error = null;
                            try {
                                cameraManager.currentConfiguration = data.cameraData.Value.configToSet?.Value;
                                Texture2DSerializable.ClearCache();
                            } catch (Exception e) {
                                error = e.ToString();
                            }

                            send(new CameraData {
                                error = error
                            });
                        });
                        break;
                    default:
                        throw new Exception();
                }
            }
        }

        void waitForAsyncConversionsToFinish( Action action) {
            DontDestroyOnLoadSingleton.AddCoroutine(waitForAsyncConversionsToFinishCor(action), nameof(waitForAsyncConversionsToFinishCor));
        }

        IEnumerator waitForAsyncConversionsToFinishCor([NotNull] Action action) {
            canRunAsyncConversion = false;
            while (asyncConversionCoroutineNames.Any()) {
                yield return null;
            }

            action();
            canRunAsyncConversion = true;
        }
        
        /// ARCore can't set currentConfiguration while image conversion is running:
        /// Exception: System.InvalidOperationException: Cannot set camera configuration because you have not disposed of all XRCpuImage and allowed all asynchronous conversion jobs to complete
        public bool canRunAsyncConversion { get; private set; } = true;

        readonly List<string> asyncConversionCoroutineNames = new List<string>();
        
        public void AddAsyncConversionCoroutine(IEnumerator cor, string name) {
            DontDestroyOnLoadSingleton.AddCoroutine(asyncConversionCoroutine(cor, name), name);
        }

        IEnumerator asyncConversionCoroutine(IEnumerator cor, string name) {
            asyncConversionCoroutineNames.Add(name);
            while (cor.MoveNext()) {
                yield return cor.Current;
            }

            var removed = asyncConversionCoroutineNames.Remove(name);
            Assert.IsTrue(removed);
        }

        [Conditional("_")]
        void logConfigs(string s) {
            Debug.Log($"{nameof(CameraSubsystemSender)} log configs: {s}");
        }
        
        CameraConfigurationSerializable? serializeCurrentConfiguration() {
            var config = getCurrentConfig();
            if (config.HasValue) {
                return CameraConfigurationSerializable.Create(config.Value);
            } else {
                return null;
            }
        }

        XRCameraConfiguration? getCurrentConfig() {
            if (needResetConfig) {
                needResetConfig = false;
                
                if (cameraManager.descriptor.supportsCameraConfigurations) {
                    using (var configs = cameraManager.GetConfigurations(Allocator.Temp)) {
                        if (configs.Any()) {
                            var first = configs.First();
                            waitForAsyncConversionsToFinish(() => {
                                try {
                                    logConfigs($"set first config {first.ToString()}");
                                    cameraManager.currentConfiguration = first;
                                    // Assert.AreEqual(first, cameraManager.currentConfiguration); // currentConfiguration setter is async
                                } catch (Exception e) {
                                    Debug.LogError($"set cameraManager.currentConfiguration failed with exception: {e}");
                                }    
                            });
                            
                            return first;
                        } else {
                            logConfigs("no configs");
                        }
                    }
                }
            }

            return cameraManager.currentConfiguration;
        }
        
        CameraConfigurations serializeConfigurations() {
            if (cameraManager.descriptor.supportsCameraConfigurations) {
                using (var configs = cameraManager.GetConfigurations(Allocator.Temp)) {
                    return new CameraConfigurations {
                        isSupported = true,
                        configs = configs.Select(CameraConfigurationSerializable.Create).ToArray()
                    };
                }
            } else {
                return new CameraConfigurations {
                    isSupported = false
                };
            }
        }
    }

    
    public static class DebugUtils {
        static readonly HashSet<string> errors = new HashSet<string>();
            
        public static void LogErrorOnce(string msg) {
            if (errors.Add(msg)) {
                Debug.LogError(msg);
            }
        }
    }

    
    [Serializable]
    public struct CameraConfigurationSerializable {
        public int x;
        public int y;
        public int m_Framerate;
        public IntPtr m_NativeConfigurationHandle;

        
        public static CameraConfigurationSerializable? Create(XRCameraConfiguration? _) {
            if (_.HasValue) {
                return Create(_.Value);
            } else {
                return null;
            }
        }

        public static CameraConfigurationSerializable Create(XRCameraConfiguration _) {
            return new CameraConfigurationSerializable {
                x = _.resolution.x,
                y = _.resolution.y,
                m_Framerate = _.framerate ?? 0,
                #if ARFOUNDATION_4_0_OR_NEWER
                    m_NativeConfigurationHandle = _.nativeConfigurationHandle
                #endif
            };
        }

        static readonly Reflector<XRCameraConfiguration> reflector = new Reflector<XRCameraConfiguration>();

        public XRCameraConfiguration Value {
            get {
                var result = reflector.GetResultBuilder();
                result.SetField("m_Resolution", new Vector2Int(x,y));
                result.SetField(nameof(m_Framerate), m_Framerate);
                #if ARFOUNDATION_4_0_OR_NEWER
                    result.SetField(nameof(m_NativeConfigurationHandle), m_NativeConfigurationHandle);
                #endif
                return result.Result;
            }
        }
    }

    [Serializable]
    public struct CameraConfigurations {
        [CanBeNull] public CameraConfigurationSerializable[] configs;
        public bool isSupported;
    }

    
    [Serializable]
    public struct ARCameraFrameEventArgsSerializable {
        [NotNull] public SerializedTextureAndPropId[] textures;
        public XRCameraFrameSerializable frame;
        public bool invertCulling;


        public static ARCameraFrameEventArgsSerializable Create(ARCameraFrameEventArgs args) {
            return new ARCameraFrameEventArgsSerializable {
                textures = serializeTextures(args),
                frame = XRCameraFrameSerializable.Create(args),
                invertCulling = CameraSubsystemSender.Instance.cameraManager.subsystem.invertCulling
            };
        }

        [NotNull]
        static SerializedTextureAndPropId[] serializeTextures(ARCameraFrameEventArgs args) {
            var textures = args.textures;
            if (textures == null || !Settings.EnableBackgroundVideo) {
                return new SerializedTextureAndPropId[0];
            }
            
            return textures
                .Select((texture, index) => new SerializedTextureAndPropId {
                    texture = Texture2DSerializable.Create(texture, true, Settings.cameraVideoSettings.resolutionScale),
                    propName = CameraSubsystemSender.Instance.PropIdToName(args.propertyNameIds[index])
                }).ToArray();
        }
    }

    [Serializable]
    public struct XRCameraFrameSerializable {
        long timestampNs;
        float averageBrightness;
        float averageColorTemperature;
        ColorSerializable colorCorrection;
        Matrix4x4Serializable projectionMatrix;
        Matrix4x4Serializable displayMatrix;
        TrackingState trackingState;
        IntPtr nativePtr;
        XRCameraFrameProperties properties;
        float averageIntensityInLumens;
        double exposureDuration;
        float exposureOffset;
        #if ARFOUNDATION_4_0_OR_NEWER
            float m_MainLightIntensityLumens;
            ColorSerializable m_MainLightColor;
            Vector3Serializable m_MainLightDirection;
            SphericalHarmonicsL2Serializable m_AmbientSphericalHarmonics;
            // float noiseIntensity; // available only in AR Foundation >= 4.0.2
        #endif


        public static XRCameraFrameSerializable Create(ARCameraFrameEventArgs a) {
            var lightEstimation = a.lightEstimation;
            return new XRCameraFrameSerializable {
                timestampNs = a.timestampNs ?? 0,
                averageBrightness = lightEstimation.averageBrightness ?? 0,
                averageColorTemperature = lightEstimation.averageColorTemperature ?? 0,
                colorCorrection = ColorSerializable.Create(lightEstimation.colorCorrection ?? new Color()),
                projectionMatrix = Matrix4x4Serializable.Create(a.projectionMatrix ?? Matrix4x4.zero),
                displayMatrix = Matrix4x4Serializable.Create(a.displayMatrix ?? Matrix4x4.zero),
                trackingState = TrackingState.Tracking,
                nativePtr = IntPtr.Zero,
                properties = getProps(a),
                averageIntensityInLumens = lightEstimation.averageIntensityInLumens ?? 0,
                exposureDuration = a.exposureDuration ?? 0,
                exposureOffset = a.exposureOffset ?? 0,
                #if ARFOUNDATION_4_0_OR_NEWER
                    m_MainLightIntensityLumens = lightEstimation.mainLightIntensityLumens ?? 0,
                    m_MainLightColor = ColorSerializable.Create(lightEstimation.mainLightColor ?? new Color()),
                    m_MainLightDirection = Vector3Serializable.Create(lightEstimation.mainLightDirection ?? Vector3.zero),
                    m_AmbientSphericalHarmonics = SphericalHarmonicsL2Serializable.Create(lightEstimation.ambientSphericalHarmonics ?? new SphericalHarmonicsL2()),
                    // noiseIntensity = a.noiseIntensity
                #endif
            };
        }

        static XRCameraFrameProperties getProps(ARCameraFrameEventArgs a) {
            var lightEstimation = a.lightEstimation;
            var props = new XRCameraFrameProperties();
            if (a.timestampNs.HasValue) {
                props |= XRCameraFrameProperties.Timestamp;
            }

            if (lightEstimation.averageBrightness.HasValue) {
                props |= XRCameraFrameProperties.AverageBrightness;
            }

            if (lightEstimation.averageColorTemperature.HasValue) {
                props |= XRCameraFrameProperties.AverageColorTemperature;
            }

            if (lightEstimation.colorCorrection.HasValue) {
                props |= XRCameraFrameProperties.ColorCorrection;
            }
            
            if (a.projectionMatrix.HasValue) {
                props |= XRCameraFrameProperties.ProjectionMatrix;
            }
            
            if (a.displayMatrix.HasValue) {
                props |= XRCameraFrameProperties.DisplayMatrix;
            }
            
            if (lightEstimation.averageIntensityInLumens.HasValue) {
                props |= XRCameraFrameProperties.AverageIntensityInLumens;
            }
            
            if (a.exposureDuration.HasValue) {
                props |= XRCameraFrameProperties.ExposureDuration;
            }
            
            if (a.exposureOffset.HasValue) {
                props |= XRCameraFrameProperties.ExposureOffset;
            }
            
            #if ARFOUNDATION_4_0_OR_NEWER
                if (lightEstimation.mainLightDirection.HasValue) {
                    props |= XRCameraFrameProperties.MainLightDirection;
                }

                if (lightEstimation.mainLightColor.HasValue) {
                    props |= XRCameraFrameProperties.MainLightColor;
                }

                if (lightEstimation.mainLightIntensityLumens.HasValue) {
                    props |= XRCameraFrameProperties.MainLightIntensityLumens;
                }

                if (lightEstimation.ambientSphericalHarmonics.HasValue) {
                    props |= XRCameraFrameProperties.AmbientSphericalHarmonics;
                }

                // props |= XRCameraFrameProperties.NoiseIntensity;
            #endif

            return props;
        }

        public XRCameraFrame Value {
            get {
                object boxed = new XRCameraFrame();
                setValue(boxed, "m_TimestampNs", timestampNs);
                setValue(boxed, "m_AverageBrightness", averageBrightness);
                setValue(boxed, "m_AverageColorTemperature", averageColorTemperature);
                setValue(boxed, "m_ColorCorrection", colorCorrection.Value);
                setValue(boxed, "m_ProjectionMatrix", projectionMatrix.Value);
                setValue(boxed, "m_DisplayMatrix", displayMatrix.Value);
                setValue(boxed, "m_TrackingState", trackingState);
                setValue(boxed, "m_NativePtr", nativePtr);
                setValue(boxed, "m_Properties", properties);
                setValue(boxed, "m_AverageIntensityInLumens", averageIntensityInLumens);
                setValue(boxed, "m_ExposureDuration", exposureDuration);
                setValue(boxed, "m_ExposureOffset", exposureOffset);
                #if ARFOUNDATION_4_0_OR_NEWER
                    setValue(boxed, "m_MainLightIntensityLumens", m_MainLightIntensityLumens);
                    setValue(boxed, "m_MainLightColor", m_MainLightColor.Value);
                    setValue(boxed, "m_MainLightDirection", m_MainLightDirection.Value);
                    setValue(boxed, "m_AmbientSphericalHarmonics", m_AmbientSphericalHarmonics.Value);
                    // setValue(boxed, "m_NoiseIntensity", noiseIntensity);
                #endif
                return (XRCameraFrame) boxed;
            }
        }
        
        static readonly Dictionary<string, FieldInfo> fields = typeof(XRCameraFrame).GetFields(BindingFlags.GetField | BindingFlags.Instance | BindingFlags.NonPublic).ToDictionary(_ => _.Name);

        static void setValue(object obj, string fieldName, object val) {
            fields[fieldName].SetValue(obj, val);
        }
    }

    [Serializable]
    struct Matrix4x4Serializable {
        Vector4Serializable v1, v2, v3, v4;
        
        
        public static Matrix4x4Serializable Create(Matrix4x4 m) {
            return new Matrix4x4Serializable {
                v1 = Vector4Serializable.Create(m.GetColumn(0)),
                v2 = Vector4Serializable.Create(m.GetColumn(1)),
                v3 = Vector4Serializable.Create(m.GetColumn(2)),
                v4 = Vector4Serializable.Create(m.GetColumn(3))
            };
        }
        
        public Matrix4x4 Value => new Matrix4x4(v1.Value,v2.Value,v3.Value,v4.Value);
    }
    
    [Serializable]
    public struct Vector4Serializable {
        float x, y, z, w;

        public static Vector4Serializable Create(Vector4 v) {
            return new Vector4Serializable {
                x = v.x,
                y = v.y,
                z = v.z,
                w = v.w
            };
        }

        public Vector4 Value => new Vector4(x, y, z, w);
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct ColorSerializable {
        float r, g, b, a;

        public static ColorSerializable Create(Color c) {
            return new ColorSerializable {
                r = c.r,
                g = c.g,
                b = c.b,
                a = c.a
            };
        }

        public Color Value => new Color(r, g, b, a);
    }

    [Serializable]
    public struct CameraData {
        public ARCameraFrameEventArgsSerializable? cameraFrame;
        public CameraConfigurations? allConfigurations;
        public CameraConfigurationSerializable? currentConfiguration;
        [CanBeNull] public string error;
    }

    [Serializable]
    public struct CameraDataEditor {
        public CameraDataEditorRequest? request;
        public CameraConfigurationSerializable? configToSet;
    }


    public enum CameraDataEditorRequest {
        GetAllConfigurations,
        GetCurrentConfiguration,
        SetCurrentConfiguration
    }
    
        
    [Serializable]
    public struct SerializedTextureAndPropId {
        [NotNull] public Texture2DSerializable texture;
        [NotNull] public string propName;
    }
}
