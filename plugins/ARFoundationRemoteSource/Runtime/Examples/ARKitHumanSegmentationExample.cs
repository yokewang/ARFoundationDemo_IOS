using UnityEngine;


namespace ARFoundationRemote.Runtime {
    public class ARKitHumanSegmentationExample : MonoBehaviour {
        [SerializeField] Transform plane = null;

        
        void OnGUI() {
            Sender.ShowTextAtCenter(getText());
        }

        string getText() {
            return Defines.isIOS ? $"Only human body that is closer than {plane.localPosition.z}\nmeters away will be visible." :
                "Human segmentation is only supported on iOS";
        }
    }
}
