#if UNITY_EDITOR
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Object = UnityEngine.Object;

#if XR_MANAGEMENT_3_2_10_OR_NEWER
    using UnityEngine.XR.Management;
#endif


namespace ARFoundationRemote.Runtime {
    public partial class SessionSubsystem : XRSessionSubsystem, IReceiver {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void RegisterDescriptor() {
            //Debug.Log("RegisterDescriptor ARemoteSessionSubsystem");
            var thisType = typeof(SessionSubsystem);
            XRSessionSubsystemDescriptor.RegisterDescriptor(new XRSessionSubsystemDescriptor.Cinfo {
                id = thisType.Name,
                #if UNITY_2020_2_OR_NEWER
                    providerType = typeof(ARemoteSessionSubsystemProvider),
                    subsystemTypeOverride = thisType,
                #else
                    subsystemImplementationType = thisType
                #endif
            });
        }

        #if !UNITY_2020_2_OR_NEWER
        protected override Provider CreateProvider() => new ARemoteSessionSubsystemProvider();
        #endif
        
        class ARemoteSessionSubsystemProvider : Provider {
            public override Promise<SessionAvailability> GetAvailabilityAsync() {
                return Promise<SessionAvailability>.CreateResolvedPromise(SessionAvailability.Supported | SessionAvailability.Installed);
            }

            public override TrackingState trackingState => TrackingState;

            public override void Reset() {
                SendMessageToRemote(EditorToPlayerMessageType.ResetSession);
            }

            
            public override void 
                #if UNITY_2020_2_OR_NEWER
                    Start
                #else
                    Resume
                #endif
                () {
                OnSessionStart();
                ResumeRemoteSession();
            }

            void ResumeRemoteSession() {
                DontDestroyOnLoadSingleton.Instance.StartCoroutine(startRemoteSessionCor());
            }

            IEnumerator startRemoteSessionCor() {
                // todo do we actually need this?
                while (state < ARemoteReceiverState.Running) {
                    yield return null;                    
                }
                
                SendMessageToRemote(EditorToPlayerMessageType.ResumeSession);
            }

            public override void 
                #if UNITY_2020_2_OR_NEWER
                    Stop
                #else
                    Pause
                #endif
                () {
                SendMessageToRemote(EditorToPlayerMessageType.PauseSession);
            }

            public override void Destroy() {
                SendMessageToRemote(EditorToPlayerMessageType.StopSession);
            }

            void SendMessageToRemote(EditorToPlayerMessageType messageType) {
                new EditorToPlayerMessage {messageType = messageType}.Send();
            }

            #if ARFOUNDATION_4_0_OR_NEWER
                Feature? trackingMode = Feature.None;

                public override Feature currentTrackingMode => trackingMode ?? Feature.None;

                public override Feature requestedTrackingMode {
                    get => currentTrackingMode;
                    set {
                        if (trackingMode != value) {
                            trackingMode = value;
                            Sender.logSceneSpecific($"send trackingMode {trackingMode}");
                            new EditorToPlayerMessage {trackingMode = value}.Send();

                            if (value == Feature.PositionAndRotation) {
                                var faceManager = Object.FindObjectOfType<ARFaceManager>();
                                if (faceManager != null && faceManager.enabled) {
                                    Debug.LogError(
@"Bug in AR Foundation: camera position and rotation will not receive updates with world camera and session state will always be <= SessionInitializing if all these conditions are met:
	- your AR device supports face tracking with 6 degrees of freedom
	- ARFaceManager is enabled
	- requestedTrackingMode is set to TrackingMode.PositionAndRotation
	- AR Foundation >= 4.0
");
                                }
                            }
                        }
                    }
                }
            #endif
        }

        static ARSessionState remoteSessionState = ARSessionState.None;
        public static ARemoteReceiverState state;
        static Action OnSessionStart;

        public static void SetOnSessionStartDelegate(Action action) {
            Assert.IsNull(OnSessionStart);
            OnSessionStart = action;
        }
        
        static TrackingState TrackingState {
            get {
                switch (remoteSessionState) {
                    case ARSessionState.SessionInitializing:
                        return TrackingState.Limited;
                    case ARSessionState.SessionTracking:
                        return TrackingState.Tracking;
                    default:
                        return TrackingState.None;
                }
            }
        }

        public void Receive(PlayerToEditorMessage data) {
            var receivedSessionState = data.sessionState;
            if (receivedSessionState.HasValue) {
                remoteSessionState = receivedSessionState.Value;
                //print("receivedSessionState " + receivedSessionState.Value);
            }
            
            #if UNITY_IOS && ARKIT_INSTALLED
                receiveWorldMap(data);
            #endif
        }
    }
    
    
    public enum ARemoteReceiverState {
        None,
        WaitingForConnectedPlayer,
        WaitingForPlayerResponse,
        Running
    }
}
#endif
