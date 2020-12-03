#if ARFOUNDATION_4_0_OR_NEWER
using UnityEngine.XR.ARFoundation;
#if ARFOUNDATION_4_1_OR_NEWER
    using System.Collections;
    using UnityEngine;
    using UnityEngine.Assertions;
    using UnityEngine.XR.ARSubsystems;
#endif


namespace ARFoundationRemote.Runtime {
    /// <summary>
    /// I wasn't able to serialize depth texture with RenderTexture:
    /// - it seems like depth information is being lost
    /// - setting RenderTexture.depth to 24/32 is not helping
    /// - I tried to set RenderTexture format to RenderTextureFormat.RFloat (the correct format from XRCpuImageFormatExtensions.AsTextureFormat for DepthUint16)
    ///
    /// So I use TryAcquireEnvironmentDepthCpuImage()
    /// </summary>
    class DepthImageSerializer {
        public bool IsDone { get; private set; }
        public SerializedTextureAndPropId? result { get; private set; }

        readonly AROcclusionManager manager;

        
        public DepthImageSerializer(AROcclusionFrameEventArgs args, AROcclusionManager manager) {
            this.manager = manager;
            if (!trySerializeDepthImage(args)) {
                IsDone = true;
            }
        }

        bool trySerializeDepthImage(AROcclusionFrameEventArgs args) {
             #if ARFOUNDATION_4_1_OR_NEWER
                if (!manager.descriptor.supportsEnvironmentDepthImage) {
                    return false;
                }

                var tex = manager.environmentDepthTexture;
                if (tex == null) {
                    return false;
                }
                
                var propName = OcclusionSubsystemSender.findPropName(tex, args);
                if (propName == null) {
                    return false;
                }
            
                if (manager.TryAcquireEnvironmentDepthCpuImage(out var image) && image.valid) {
                    DontDestroyOnLoadSingleton.AddCoroutine(serializeDepthImage(image, propName), nameof(serializeDepthImage));
                    return true;
                } else {
                    return false;
                }
            #else
                return false;
            #endif
        }

        #if ARFOUNDATION_4_1_OR_NEWER
        IEnumerator serializeDepthImage(XRCpuImage image, string propName) {
            var resolutionMultiplier = Settings.occlusionSettings.resolutionScale;
            var origWidth = image.width;
            var origHeight = image.height;
            var destWidth = Mathf.RoundToInt(origWidth * resolutionMultiplier);
            var destHeight = Mathf.RoundToInt(origHeight * resolutionMultiplier);

            var format = image.format.AsTextureFormat();
            Assert.AreNotEqual(0, (int) format, "AreNotEqual(0, (int) format)");
            var conversionParams = new XRCpuImage.ConversionParams {
                outputDimensions = new Vector2Int(destWidth, destHeight),
                inputRect = new RectInt(0, 0, origWidth, origHeight),
                transformation = XRCpuImage.Transformation.None,
                outputFormat = format
            };

            using (image) {
                // callback version of ConvertAsync() will never finish after ARSession pause
                using (var conversion = image.ConvertAsync(conversionParams)) {
                    yield return conversion;
                    if (conversion.status == XRCpuImage.AsyncConversionStatus.Ready) {
                        var data = conversion.GetData<byte>();
                        if (data.IsCreated) {
                            result = new SerializedTextureAndPropId {
                                texture = new Texture2DSerializable {
                                    data = data.ToArray(),
                                    compressed = false,
                                    format = format,
                                    width = destWidth,
                                    height = destHeight
                                },
                                propName = propName
                            };    
                        }
                    }

                    IsDone = true;
                }
            }
        }
        #endif
    }
}
#endif
