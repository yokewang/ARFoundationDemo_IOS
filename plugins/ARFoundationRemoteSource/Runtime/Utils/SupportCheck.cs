#if UNITY_EDITOR
using JetBrains.Annotations;
using UnityEngine;


namespace ARFoundationRemote.Runtime {
    public class SupportCheck : MonoBehaviour {
        public static bool CheckCameraAndOcclusionSupport([NotNull] Material cameraMaterial, [NotNull] string textureName) {
            if (!cameraMaterial.shader.isSupported) {
                Debug.LogError("Background camera material shader is not supported in Editor.");
                return false;
            }

            if (!cameraMaterial.HasProperty(textureName)) {
                Debug.LogError("Background camera material doesn't contain property with name " + textureName +
                               ". Please ensure AR Companion app is running the same build target and same render pipeline as Editor.");
                return false;
            }

            if (Defines.isUnity2019_2 && Application.platform == RuntimePlatform.WindowsEditor) {
                // Unity Editor crashes at
                // ARTextureInfo.CreateTexture() at Texture2D.CreateExternalTexture()
                Debug.LogError("Camera video is not supported in Windows Unity Editor 2019.2");
            }

            return true;
        }
    }
}
#endif
