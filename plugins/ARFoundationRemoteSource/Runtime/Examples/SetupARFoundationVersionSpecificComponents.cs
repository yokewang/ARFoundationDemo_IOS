#if ARFOUNDATION_4_0_OR_NEWER
    using System;
#endif
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.XR.ARFoundation;


namespace ARFoundationRemote.Runtime {
    public class SetupARFoundationVersionSpecificComponents : MonoBehaviour {
        [SerializeField] ARSessionOrigin origin = null;
        [SerializeField] ARSession arSession = null;
        [SerializeField] bool isUserFacing = false;
        [SerializeField] bool enableLightEstimation = true; // todo enable only requested features
        [SerializeField] TrackingModeWrapper trackingMode = TrackingModeWrapper.DontCare;

        [CanBeNull] ARCameraManager _cameraManager = null;
        [CanBeNull] ARCameraBackground _cameraBackground = null;
        bool initialized;

        
        [NotNull]
        public ARCameraManager cameraManager {
            get {
                if (_cameraManager == null) {
                    init();
                }

                Assert.IsNotNull(_cameraManager);
                return _cameraManager;
            }
        }

        [NotNull]
        public ARCameraBackground cameraBackground {
            get {
                if (_cameraBackground == null) {
                    init();
                }

                Assert.IsNotNull(_cameraBackground);
                return _cameraBackground;
            }
        }

        void Awake() {
            Assert.AreEqual(1, FindObjectsOfType<SetupARFoundationVersionSpecificComponents>().Length);
            
            if (!initialized) {
                init();
            }
        }

        void init() {
            Assert.IsFalse(initialized);
            initialized = true;
            var cameraGameObject = origin.camera.gameObject;
            cameraGameObject.SetActive(false);
            var camManager = cameraGameObject.AddComponent<ARCameraManager>();
            _cameraManager = camManager;
            _cameraBackground = cameraGameObject.AddComponent<ARCameraBackground>();

            if (isUserFacing) {
                camManager.SetCameraAutoFocus(false);
                #if ARFOUNDATION_4_0_OR_NEWER
                    camManager.requestedFacingDirection = CameraFacingDirection.User;
                #endif
            } else {
                camManager.SetCameraAutoFocus(true);
            }

            if (enableLightEstimation) {
                #if ARFOUNDATION_4_0_OR_NEWER
                    camManager.requestedLightEstimation = LightEstimation.AmbientIntensity | LightEstimation.AmbientColor | LightEstimation.MainLightDirection | LightEstimation.MainLightIntensity;
                #else
                    camManager.lightEstimationMode = UnityEngine.XR.ARSubsystems.LightEstimationMode.AmbientIntensity;
                #endif
            }

            #if ARFOUNDATION_4_0_OR_NEWER
                TrackingMode toTrackingMode(TrackingModeWrapper mode) {
                    switch (mode) {
                        case TrackingModeWrapper.DontCare:
                            return TrackingMode.DontCare;
                        case TrackingModeWrapper.RotationOnly:
                            return TrackingMode.RotationOnly;
                        case TrackingModeWrapper.PositionAndRotation:
                            return TrackingMode.PositionAndRotation;
                        default:
                            throw new Exception();
                    }
                }
                
                if (trackingMode != TrackingModeWrapper.DontSetup) {
                    arSession.requestedTrackingMode = toTrackingMode(trackingMode);
                }
            #endif
            
            cameraGameObject.SetActive(true);
        }
    }

    public static class ARCameraManagerExtensions {
        public static void SetCameraAutoFocus(this ARCameraManager cameraManager, bool auto) {
            cameraManager.
                #if ARFOUNDATION_4_0_OR_NEWER
                    autoFocusRequested = auto;
                #else
                    focusMode = auto ? UnityEngine.XR.ARSubsystems.CameraFocusMode.Auto : UnityEngine.XR.ARSubsystems.CameraFocusMode.Fixed;
                #endif
        }
    }

    public enum TrackingModeWrapper {
        DontCare,
        RotationOnly,
        PositionAndRotation,
        DontSetup
    }
}
