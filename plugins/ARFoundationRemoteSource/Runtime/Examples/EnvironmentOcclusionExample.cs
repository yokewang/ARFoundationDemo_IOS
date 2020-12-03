using UnityEngine;


namespace ARFoundationRemote.Runtime {
    public class EnvironmentOcclusionExample : MonoBehaviour {
        [SerializeField] Transform plane = null;


        void OnGUI() {
            Sender.ShowTextAtCenter(getText());
        }

        string getText() {
            if (Defines.isARFoundation4_1_OrNewer) {
                return $"Objects further than {plane.localPosition.z}\nmeters away will be clipped." + "\n" + getIOSEnvWarning();
            } else {
                return "Environment occlusion is only available in AR Foundation >= 4.1";
            }
        }

        string getIOSEnvWarning() {
            return Defines.isIOS ? "Environment occlusion only available in iOS14." : "";
        }
    }
}
