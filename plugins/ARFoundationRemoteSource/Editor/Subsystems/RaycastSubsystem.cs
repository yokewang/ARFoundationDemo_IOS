using UnityEngine;
using UnityEngine.XR.ARSubsystems;


namespace ARFoundationRemote.Editor {
    public class RaycastSubsystem : XRRaycastSubsystem {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void RegisterDescriptor() {
            var thisType = typeof(RaycastSubsystem);

            XRRaycastSubsystemDescriptor.RegisterDescriptor(new XRRaycastSubsystemDescriptor.Cinfo {
                id = thisType.Name,
                #if UNITY_2020_2_OR_NEWER
                    providerType = typeof(RaycastSubsystemProvider),
                    subsystemTypeOverride = thisType,
                #else
                    subsystemImplementationType = thisType,
                #endif
                supportedTrackableTypes =
                    TrackableType.Planes |
                    TrackableType.FeaturePoint
            });
        }

        #if !UNITY_2020_2_OR_NEWER
        protected override Provider CreateProvider() => new RaycastSubsystemProvider();
        #endif

        class RaycastSubsystemProvider: Provider {
        }
    }
}
