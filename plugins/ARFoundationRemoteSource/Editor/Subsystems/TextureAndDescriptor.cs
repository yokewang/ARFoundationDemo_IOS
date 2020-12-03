using ARFoundationRemote.Runtime;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;
using UnityEngine.XR.ARSubsystems;
using Object = UnityEngine.Object;


namespace ARFoundationRemote.Editor {
    public class TextureAndDescriptor {
        [CanBeNull] Texture2D cachedTexture;
        public XRTextureDescriptor descriptor { get; private set; }


        public void Update(SerializedTextureAndPropId ser) {
            var serializedTexture = ser.texture;
            if (cachedTexture == null) {
                cachedTexture = serializedTexture.CreateEmpty();
            } else if (!serializedTexture.CanDeserializeInto(cachedTexture)) {
                // Debug.Log("clear cached texture");
                Object.Destroy(cachedTexture);
                cachedTexture = serializedTexture.CreateEmpty();
            }

            serializedTexture.DeserializeInto(cachedTexture);

            descriptor = new XRTextureDescriptorWrapper(cachedTexture, Shader.PropertyToID(ser.propName)).Value;
            #if ARFOUNDATION_4_0_OR_NEWER
                Assert.AreEqual(TextureDimension.Tex2D, descriptor.dimension);
            #endif
        }

        public void OnDestroy() {
            descriptor.Reset();
            if (cachedTexture != null) {
                Object.Destroy(cachedTexture);
            }
        }
    }
}
