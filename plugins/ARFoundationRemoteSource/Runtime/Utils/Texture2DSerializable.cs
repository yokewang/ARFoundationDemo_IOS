using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Assertions;


namespace ARFoundationRemote.Runtime {
    [Serializable]
    public class Texture2DSerializable {
        /// forcing mipmapCount to be 1 is not working
        const int mipmapCount = 1;

        public byte[] data;
        public int width;
        public int height;
        public TextureFormat format;
        public bool compressed;


        public static Texture2DSerializable Create(Texture2D tex, bool compress, float resolutionMultiplier) {
            var w = Mathf.RoundToInt(tex.width * resolutionMultiplier);
            var h = Mathf.RoundToInt(tex.height * resolutionMultiplier);
            return new Texture2DSerializable {
                data = serialize(tex, w, h, compress),
                width = w,
                height = h,
                format = tex.format,
                compressed = compress
            };
        }

        static byte[] serialize(Texture2D tex, int w, int h, bool compress) {
            var rt = RenderTexture.GetTemporary(w, h, 0, tex.graphicsFormat);
            Assert.IsNull(RenderTexture.active);
            blit(tex, rt);
            Assert.AreEqual(rt, RenderTexture.active, "AreEqual(rt, RenderTexture.active)");
            var downsized = getDestTexture(tex, w, h);
            downsized.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0, false);
            Assert.AreEqual(tex.graphicsFormat, downsized.graphicsFormat, "AreEqual(tex.graphicsFormat, downsized.graphicsFormat)");
            Assert.AreEqual(tex.format, downsized.format, "AreEqual(tex.format, downsized.format)");
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            #if AR_FOUNDATION_REMOTE_INSTALLED
                return compress ? downsized.EncodeToJPG(Settings.cameraVideoSettings.quality) : downsized.GetRawTextureData();
            #else 
                throw new Exception();
            #endif
        }

        /// Graphics.Blit sets RenderTexture.active to dest RenderTexture
        static void blit(Texture2D tex, RenderTexture rt) {
            // I don't understand why it works differently on Android and iOS. Try to rewrite it to ARCameraManager.TryAcquireLatestCpuImage()
            if (Application.platform == RuntimePlatform.Android) {
                // this produces green screen in Editor when used with iOS
                Graphics.Blit(null, rt, CameraSubsystemSender.Instance.cameraManager.cameraMaterial);
            } else {
                // this produces gray screen in Editor when used with Android
                Graphics.Blit(tex, rt);
            }
        }

        public static void ClearCache() {
            foreach (var _ in cachedTextures) {
                UnityEngine.Object.Destroy(_);
            }

            cachedTextures.Clear();
        }

        static List<Texture2D> cachedTextures = new List<Texture2D>();

        static Texture2D getDestTexture(Texture2D tex, int w, int h) {
            var existing = cachedTextures.Find(_ => _.width == w && _.height == h && _.format == tex.format);
            if (existing != null) {
                return existing;
            } else {
                var newTex = new Texture2D(w, h, tex.format, mipmapCount, isLinear);
                cachedTextures.Add(newTex);
                return newTex;
            }
        }

        public Texture2D DeserializeTexture() {
            var result = CreateEmpty();
            DeserializeInto(result);
            return result;
        }

        public bool CanDeserializeInto([NotNull] Texture2D tex) {
            if (!compressed && tex.format != format) {
                return false;
            }

            return tex.width == width && tex.height == height;
        }
        
        public void DeserializeInto([NotNull] Texture2D tex) {
            if (compressed) {
                #if AR_FOUNDATION_REMOTE_INSTALLED
                    var isLoaded = tex.LoadImage(data);
                    Assert.IsTrue(isLoaded);
                #endif
            } else {
                tex.LoadRawTextureData(data);
                tex.Apply();
                Assert.AreEqual(format, tex.format);
            }
        }

        [NotNull]
        public Texture2D CreateEmpty() {
            return new Texture2D(width, height, format, mipmapCount, isLinear);
        }

        static bool isLinear => QualitySettings.activeColorSpace == ColorSpace.Linear;

        public override string ToString() {
            return $"{width}, {height}";
        }
    }
}
