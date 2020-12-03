#if ARFOUNDATION_4_0_OR_NEWER
using System;
using System.Collections;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using System.Linq;
using UnityEngine.XR.ARSubsystems;


namespace ARFoundationRemote.Runtime {
    public class OcclusionSubsystemSender : ISubsystemSender {
        readonly AROcclusionManager manager;
        bool isSending;
        static bool isSupportChecked;


        public OcclusionSubsystemSender(AROcclusionManager _manager) {
            manager = _manager;
            manager.frameReceived += args => {
                if (!canSend()) {
                    return;
                }

                if (isSending) {
                    return;
                }

                if (args.textures.Any() && CameraSubsystemSender.Instance.canRunAsyncConversion) {
                    // AR Companion will crash on scene reload if DepthImageSerializer is running
                    // DontDestroyOnLoadSingleton.AddCoroutine ensures that all coroutines are finished before calling scene reload
                    CameraSubsystemSender.Instance.AddAsyncConversionCoroutine(sendTextures(args), nameof(sendTextures));

                    if (!isSupportChecked) {
                        isSupportChecked = true;
                        if (!isSupported()) {
                            Sender.runningErrorMessage += $"{Constants.packageName}: occlusion is not supported on this device";
                        }
                    }
                }
            };
        }

        IEnumerator sendTextures(AROcclusionFrameEventArgs args) {
            isSending = true;

            var depthSerializator = new DepthImageSerializer(args, manager);
            while (!depthSerializator.IsDone) {
                yield return null;
            }

            var descriptor = manager.descriptor;
            new PlayerToEditorMessage {
                occlusionData = new OcclusionData {
                    humanStencil = trySerialize(descriptor.supportsHumanSegmentationStencilImage, () => manager.humanStencilTexture, args),
                    humanDepth = trySerialize(descriptor.supportsHumanSegmentationDepthImage, () => manager.humanDepthTexture, args),
                    #if ARFOUNDATION_4_1_OR_NEWER
                        environmentDepth = depthSerializator.result,
                        environmentDepthConfidence = trySerialize(descriptor.supportsEnvironmentDepthConfidenceImage, () => manager.environmentDepthConfidenceTexture, args),
                    #endif
                }
            }.Send();

            isSending = false;
        }

        SerializedTextureAndPropId? trySerialize(bool isSupported, [NotNull] Func<Texture2D> getTexture, AROcclusionFrameEventArgs args) {
            if (!isSupported) {
                return null;
            }

            var tex = getTexture();
            if (tex == null) {
                return null;
            }
            
            var propName = findPropName(tex, args);
            if (propName != null) {
                return new SerializedTextureAndPropId {
                    texture = Texture2DSerializable.Create(tex, false, Settings.occlusionSettings.resolutionScale),
                    propName = propName
                };
            } else {
                return null;
            }
        }

        float lastSendTime;
        
        bool canSend() {
            if (!Connection.senderConnection.CanSendNonCriticalMessage) {
                return false;
            }
            
            var curTime = Time.time;
            if (curTime - lastSendTime > 1f / Settings.occlusionSettings.maxFPS) {
                lastSendTime = curTime;
                return true;
            } else {
                return false;
            }
        }

        [CanBeNull]
        public static string findPropName([NotNull] Texture2D tex, AROcclusionFrameEventArgs args) {
            var i = args.textures.FindIndex(_ => _.GetNativeTexturePtr() == tex.GetNativeTexturePtr());
            if (i != -1 && CameraSubsystemSender.Instance.PropIdToName(args.propertyNameIds[i], out var result)) {
                return result;
            } else {
                return null;
            }
        }

        void ISubsystemSender.EditorMessageReceived(EditorToPlayerMessage data) {
            var occlusionData = data.occlusionData;
            if (occlusionData == null) {
                return;
            }
            
            if (occlusionData.requestedHumanDepthMode.HasValue) {
                manager.requestedHumanDepthMode = occlusionData.requestedHumanDepthMode.Value;
            }
            
            if (occlusionData.requestedHumanStencilMode.HasValue) {
                manager.requestedHumanStencilMode = occlusionData.requestedHumanStencilMode.Value;
            }

            if (occlusionData.enableOcclusion.HasValue) {
                Sender.Instance.SetManagerEnabled(manager, occlusionData.enableOcclusion.Value);
            }

            #if ARFOUNDATION_4_1_OR_NEWER
            if (occlusionData.requestedEnvironmentDepthMode.HasValue) {
                manager.requestedEnvironmentDepthMode = occlusionData.requestedEnvironmentDepthMode.Value;
            }

            if (occlusionData.requestedOcclusionPreferenceMode.HasValue) {
                manager.requestedOcclusionPreferenceMode = occlusionData.requestedOcclusionPreferenceMode.Value;
            }
            #endif
        }

        bool isSupported() {
            var descriptor = manager.descriptor;
            if (descriptor == null) {
                return false;
            }
            
            #if ARFOUNDATION_4_1_OR_NEWER
            // correct values are reported only after first AROcclusionManager.frameReceived event
            // Debug.LogWarning($"supportsEnvironmentDepthImage: {descriptor.supportsEnvironmentDepthImage}, supportsEnvironmentDepthConfidenceImage: {descriptor.supportsEnvironmentDepthConfidenceImage}");
            if (descriptor.supportsEnvironmentDepthImage || descriptor.supportsEnvironmentDepthConfidenceImage) {
                return true;
            }
            #endif

            return descriptor.supportsHumanSegmentationDepthImage || descriptor.supportsHumanSegmentationStencilImage;
        }
    }


    [Serializable]
    public class OcclusionData {
        public SerializedTextureAndPropId? humanStencil;
        public SerializedTextureAndPropId? humanDepth;
        #if ARFOUNDATION_4_1_OR_NEWER
            public SerializedTextureAndPropId? environmentDepth;
            public SerializedTextureAndPropId? environmentDepthConfidence;
        #endif
    }


    [Serializable]
    public class OcclusionDataEditor {
        public HumanSegmentationDepthMode? requestedHumanDepthMode;
        public HumanSegmentationStencilMode? requestedHumanStencilMode;
        public bool? enableOcclusion;
        #if ARFOUNDATION_4_1_OR_NEWER
        public EnvironmentDepthMode? requestedEnvironmentDepthMode;
        public OcclusionPreferenceMode? requestedOcclusionPreferenceMode;
        #endif
    }
}
#endif
