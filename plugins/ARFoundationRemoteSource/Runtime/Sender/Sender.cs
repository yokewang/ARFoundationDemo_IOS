using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;
using Debug = UnityEngine.Debug;


namespace ARFoundationRemote.Runtime {
    public class Sender : MonoBehaviour {
        [SerializeField] List<SubsystemSender> serializedSenders = new List<SubsystemSender>();
        [SerializeField] ARSession arSession = null;
        [SerializeField] ARSessionOrigin origin = null;
        [SerializeField] SetupARFoundationVersionSpecificComponents setuper = null;

        const string noARCapabilitiesMessage = "Please run this scene on device with AR capabilities\n" +
                                               "and install AR Provider (ARKit XR Plugin, ARCore XR Plugin, etc)\n" +
                                               "and enable AR Provider in Project Settings -> XR Plug-in Management";
        static readonly string[] logMessagesToIgnore = {"ARPoseDriver is already consuming data from", "valid loader configuration exists in the XR project settings"};

        public static Sender Instance { get; private set; }
        readonly List<ISubsystemSender> senders = new List<ISubsystemSender>();


        void Awake() {
            logSceneReload("Sender.Awake()");
            Application.logMessageReceivedThreaded += logMessageReceivedThreaded;
            
            var xrManagerSettings = XRGeneralSettings.Instance.Manager;
            Assert.IsNotNull(xrManagerSettings, "xrManagerSettings != null");
            if (!xrManagerSettings.isInitializationComplete) {
                xrManagerSettings.InitializeLoaderSync();
            }
            
            Assert.IsNull(Instance, "Instance == null");
            Instance = this;
            
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            senders.AddRange(serializedSenders);
            
            #if ARFOUNDATION_4_0_OR_NEWER
                var originGameObject = origin.gameObject;

                void executeOnDisabledOrigin(Action action) {
                    originGameObject.SetActive(false);
                    action();
                    originGameObject.SetActive(true);
                }

                executeOnDisabledOrigin(() => {
                    // todo add only if supported
                    var manager = originGameObject.AddComponent<ARHumanBodyManager>();
                    manager.pose2DRequested = false;
                    manager.pose3DRequested = false;
                    manager.pose3DScaleEstimationRequested = false;
                    AddSender(new HumanBodySubsystemSender(manager));
                });

                AddSender(new ObjectTrackingSubsystemSender(origin));
                
                executeOnDisabledCamera(() => {
                    var manager = origin.camera.gameObject.AddComponent<AROcclusionManager>();
                    manager.enabled = false;
                    manager.requestedHumanStencilMode = HumanSegmentationStencilMode.Disabled;
                    manager.requestedHumanDepthMode = HumanSegmentationDepthMode.Disabled;

                    #if ARFOUNDATION_4_1_OR_NEWER
                        manager.requestedEnvironmentDepthMode = EnvironmentDepthMode.Disabled;
                    #endif

                    Instance.AddSender(new OcclusionSubsystemSender(manager));
                });
                
                void executeOnDisabledCamera(Action action) {
                    var cameraGameObject = origin.camera.gameObject;
                    cameraGameObject.SetActive(false);
                    action();
                    cameraGameObject.SetActive(true);
                }
            #endif
            
            AddSender(new CameraSubsystemSender(setuper.cameraManager));
            #if UNITY_IOS && ARKIT_INSTALLED
                AddSender(new WorldMapSender(arSession));
            #endif
            
            Assert.IsTrue(FindObjectOfType<ARSessionOrigin>().camera.transform.parent.lossyScale == Vector3.one);
            if (Application.isEditor) {
                Debug.LogError("Please run this scene on AR capable device");
                enabled = false;
                return;
            }

            Connection.senderConnection.Register(editorMessageReceived);
            Connection.senderConnection.RegisterDisconnection(onDisconnectedFromEditor);
            ARSession.stateChanged += onArSessionOnStateChanged;
            AddSender(gameObject.AddComponent<OriginDataReceiver>());
            DontDestroyOnLoadSingleton.AddCoroutine(checkAvailability(), nameof(checkAvailability));
        }

        void Update() {
            foreach (var _ in senders.OfType<ISubsystemSenderUpdateable>()) {
                _.UpdateSender();
            }
        }

        void OnDestroy() {
            logIfNeeded("Sender.OnDestroy()");
            Instance = null;
            ARSession.stateChanged -= onArSessionOnStateChanged;
            Connection.senderConnection.UnregisterDisconnection(onDisconnectedFromEditor);
            Application.logMessageReceivedThreaded -= logMessageReceivedThreaded;
        }


        void logMessageReceivedThreaded(string message, string stacktrace, LogType type) {
            if (type != LogType.Log && !logMessagesToIgnore.Any(message.Contains)) {
                runningErrorMessage += $"{type}: {message}\n{stacktrace}\n";
            }
        }

        [Conditional("_")]
        static void logSceneReload(string message) {
            Debug.Log(message);
        }

        void AddSender([NotNull] ISubsystemSender subsystemSender) {
            senders.Add(subsystemSender);
        }

        void initSession() {
            DontDestroyOnLoadSingleton.AddCoroutine(initSessionCor(), nameof(initSessionCor));
        }

        void onArSessionOnStateChanged(ARSessionStateChangedEventArgs args) {
            new PlayerToEditorMessage {sessionState = args.state}.Send();
        }

        IEnumerator initSessionCor() {
            while (ARSession.state < ARSessionState.Ready) {
                yield return null;
            }
            
            new PlayerToEditorMessage {messageType = PlayerToEditorMessageType.SessionReady}.Send();
        }

        void onDisconnectedFromEditor(int _) {
            logIfNeeded("onDisconnectedFromEditor");
            editorMessageReceived(new EditorToPlayerMessage{messageType = EditorToPlayerMessageType.StopSessionAndTryReloadScene});
            stopSession();
            StartCoroutine(reloadSceneCor());
        }

        IEnumerator reloadSceneCor() {
            logSceneReload("reloadSceneCor()");
            var timeStart = Time.time;
            while (DontDestroyOnLoadSingleton.runningCoroutineNames.Count > 0) {
                if (Time.time - timeStart > 5) {
                    Debug.LogError($"reloadSceneCor() failed because coroutines were running: {string.Join(", ", DontDestroyOnLoadSingleton.runningCoroutineNames)}");
                    yield break;
                }
                
                yield return null;
            }
            
            logSceneReload("LoaderUtility.Deinitialize()");
            // LoaderUtility.Deinitialize() is needed to reset ARWorldMap to initial state after calling ApplyWorldMap()
            // for consistency, execute this even if the ARWorldMap was never used 
            var xrManagerSettings = XRGeneralSettings.Instance.Manager;
            xrManagerSettings.DeinitializeLoader();
            SceneManager.LoadScene("ARCompanion");
            xrManagerSettings.InitializeLoaderSync();
        }
        
        void editorMessageReceived([NotNull] EditorToPlayerMessage data) {
            var settings = data.settings;
            if (settings != null) {
                Texture2DSerializable.ClearCache();
                Settings.Instance.arCompanionSettings = settings;
            }
            
            var messageType = data.messageType;
            if (messageType != EditorToPlayerMessageType.None) {
                logIfNeeded("editorMessageReceived type: " + messageType);
            }
            
            switch (messageType) {
                case EditorToPlayerMessageType.Init:
                    initSession();
                    break;
                case EditorToPlayerMessageType.ResumeSession:
                    setSessionEnabled(true);
                    setARComponentsEnabled(true);
                    break;
                case EditorToPlayerMessageType.PauseSession:
                    pauseSession();
                    break;
                case EditorToPlayerMessageType.ResetSession:
                    resetSession();
                    break;
                case EditorToPlayerMessageType.StopSession:
                    stopSession();
                    break;
            }

            foreach (var _ in senders) {
                _.EditorMessageReceived(data);
            }
   
            #if ARFOUNDATION_4_0_OR_NEWER
                var trackingMode = data.trackingMode;
                if (trackingMode.HasValue) {
                    var requestedTrackingMode = toTrackingMode(trackingMode.Value);
                    logSceneSpecific($"receive requestedTrackingMode {requestedTrackingMode}");
                    arSession.requestedTrackingMode = requestedTrackingMode;
                }
            #endif
        }

        void setSessionEnabled(bool isEnabled) {
            LogObjectTrackingCrash($"setSessionEnabled {isEnabled}");
            arSession.enabled = isEnabled;
        }

        void stopSession() {
            logSceneReload("stopSession()");
            pauseAndResetSession();
            setARComponentsEnabled(false);
            setManagersEnabled(false);
        }

        readonly Dictionary<Behaviour, bool> managers = new Dictionary<Behaviour, bool>();
        
        void setARComponentsEnabled(bool enable) {
            // logSceneReload($"setARComponentsEnabled {enable}");
            var types = new[] {
                typeof(ARAnchorManager), 
                typeof(ARCameraManager), 
                typeof(ARCameraBackground),
                typeof(ARInputManager)
            };
            
            foreach (var _ in types.Select(FindObjectOfType).Cast<MonoBehaviour>()) {
                _.enabled = enable;
            }
        }

        void setManagersEnabled(bool enable) {
            logSceneSpecific($"setManagersEnabled {enable}");
            foreach (var pair in managers) {
                pair.Key.enabled = enable && pair.Value;
            }
        }

        void resetSession() {
            if (Settings.debugSettingsRuntime.canResetSession) {
                LogObjectTrackingCrash("resetSession()");
                arSession.Reset();    
            }
        }

        #if ARFOUNDATION_4_0_OR_NEWER
        TrackingMode toTrackingMode(Feature f) {
            switch (f) {
                case Feature.RotationOnly:
                    return TrackingMode.RotationOnly;
                case Feature.PositionAndRotation:
                    return TrackingMode.PositionAndRotation;
                default:
                    return TrackingMode.DontCare;
            }
        }
        #endif

        public void SetManagerEnabled<T>(T manager, bool managerEnabled) where T : Behaviour {
            logSceneSpecific($"{typeof(T)} enabled {managerEnabled}");
            manager.enabled = managerEnabled;
            managers[manager] = managerEnabled;
        }

        void pauseSession() {
            setSessionEnabled(false);
        }

        IEnumerator checkAvailability() {
            yield return ARSession.CheckAvailability();
            Assert.IsTrue(isSupported, noARCapabilitiesMessage);
            pauseAndResetSession();

            if (Settings.Instance.debugSettings.printCompanionAppIPsToConsole) {
                while (true) {
                    var ips = getLocalEthernetIPAddresses().ToList();
                    if (ips.Any() && Connection.senderConnection.isActive) {
                        Debug.Log(getIPsMessage(ips));
                        break;
                    } else {
                        yield return null;
                    }
                }    
            }
        }

        void pauseAndResetSession() {
            logSceneReload("pauseAndResetSession()");
            pauseSession();
            resetSession();
        }

        static bool isSupported => ARSession.state >= ARSessionState.Ready;
        public bool IsConnectedAndRunning => Connection.senderConnection.isConnected && isSessionCreatedAndRunning;

        [Conditional("_")]
        void logIfNeeded(string message) {
            Debug.Log(message);
        }

        void OnGUI() {
            ShowTextAtCenter(getUserMessageAndAppendErrorIfNeeded());
        }

        public static string waitingErrorMessage = "";
        public static string runningErrorMessage = "";

        string getUserMessageAndAppendErrorIfNeeded() {
            if (isSessionCreatedAndRunning) {
                return runningErrorMessage;
            } else {
                return getWaitingMessage() + "\n\n" + waitingErrorMessage + "\n\n" + runningErrorMessage + "\n\nPlease leave an honest review on the Asset Store :)";
            }
        }

        string getWaitingMessage() {
            if (!isSupported) {
                return noARCapabilitiesMessage;
            } else {
                var ips = getLocalEthernetIPAddresses().ToList();
                if (ips.Any()) {
                    var server = Connection.senderConnection;
                    Assert.IsNotNull(server);
                    if (server.isActive) {
                        return getIPsMessage(ips);
                    } else {
                        return "AR Companion app can't start server.\n" +
                               "Please ensure only one instance of the app is running or restart the app.";
                    }
                } else {
                    return "Can't start sender. Please connect AR device to private network.";
                }
            }
        }

        static string getIPsMessage([NotNull] List<IPAddress> ips) {
            return "Please enter AR Companion app IP in\n" +
                   "Assets/Plugins/ARFoundationRemoteInstaller/Resources/Settings\n" +
                   "and start AR scene in Editor.\n\n" +
                   "Available IP addresses:\n" + String.Join("\n", ips);
        }

        [NotNull]
        static IEnumerable<IPAddress> getLocalEthernetIPAddresses() {
            return NetworkInterface.GetAllNetworkInterfaces()
                .SelectMany(_ => _.GetIPProperties().UnicastAddresses)
                .Select(_ => _.Address)
                .Where(_ => _.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(_))
                .Distinct();
        }

        bool isSessionCreatedAndRunning => arSession != null && arSession.enabled;

        [Conditional("_")]
        public static void logSceneSpecific(string msg) {
            Debug.Log(msg);
        }

        public static void ShowTextAtCenter(string text) {
            #if AR_FOUNDATION_REMOTE_INSTALLED
            GUILayout.BeginArea(new Rect(0, 0, Screen.width, Screen.height));
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label(text, new GUIStyle {fontSize = 30, normal = new GUIStyleState {textColor = Color.white}});
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.EndArea();
            #endif
        }

        [Conditional("_")]
        public static void LogObjectTrackingCrash(string msg) {
            Debug.Log(msg);
        }
    }


    [Serializable]
    public class PlayerToEditorMessage {
        public static readonly Guid id = new Guid("87713f79-9c4f-4595-9469-7248713ecb1d");
        public PlayerToEditorMessageType messageType;
        public PoseSerializable? cameraPose;
        [CanBeNull] public PlanesUpdateData planesUpdateData;
        [CanBeNull] public PointCloudData pointCloudData;
        public ARSessionState? sessionState;
        [CanBeNull] public FaceSubsystemData faceSubsystemData;
        public Guid? requestGuid;
        public Guid? responseGuid;
        [CanBeNull] public TouchSerializable[] touches;
        public TrackableChangesData<XRTrackedImageSerializable>? trackedImagesData;
        public CameraData? cameraData;
        public TrackableChangesData<ARAnchorSerializable>? anchorSubsystemData;
        public AnchorSubsystemMethodsResponse? anchorSubsystemMethodsResponse;
        public MeshingDataPlayer? meshingData;
        #if (UNITY_IOS || UNITY_EDITOR) && ARKIT_INSTALLED
            public WorldMapData? worldMapData;
        #endif
        #if ARFOUNDATION_4_0_OR_NEWER
            [CanBeNull] public OcclusionData occlusionData;
            public HumanBodyData? humanBodyData;
            public ObjectTrackingData? objectTrackingData;
        #endif

        public void Send() {
            Connection.senderConnection.Send(this);
        }
    }


    public enum PlayerToEditorMessageType {
        None,
        SessionReady
    }


    [Serializable]
    public class EditorToPlayerMessage {
        public static readonly Guid id = new Guid("a0aa0e9d-a0a1-4569-b44f-e9609cc8698e");
        public EditorToPlayerMessageType messageType;
        public PlaneDetectionMode? planeDetectionMode;
        public Guid? requestGuid;
        [CanBeNull] public ImageLibrarySerializableContainer imageLibrary;
        [CanBeNull] public XRReferenceImageSerializable imageToAdd;
        public SessionOriginData? sessionOriginData;
        public bool? enablePlaneSubsystem;
        public bool? enableDepthSubsystem;
        public bool? enableFaceSubsystem;
        public bool? enableImageTracking;
        public AnchorDataEditor? anchorsData;
        public int? requestedLightEstimation;
        public MeshingDataEditor? meshingData;
        #if ARFOUNDATION_4_0_OR_NEWER
            public Feature? requestedCamera;
            public Feature? trackingMode;
            [CanBeNull] public OcclusionDataEditor occlusionData;
            public HumanBodyDataEditor? humanBodyData;
            public ObjectTrackingDataEditor? objectTrackingData;
        #endif
        [CanBeNull] public ARCompanionSettings settings;
        public CameraDataEditor? cameraData;
        #if (UNITY_IOS || UNITY_EDITOR) && ARKIT_INSTALLED
            public WorldMapDataEditor? worldMapData;
        #endif
    }
    

    public enum EditorToPlayerMessageType {
        None,
        Init,
        ResumeSession,
        PauseSession,
        ResetSession,
        StopSession,
        StopSessionAndTryReloadScene
    }


    public static class EditorToPlayerMessageTypeExtensions {
        public static bool IsStop(this EditorToPlayerMessageType _) {
            switch (_) {
                case EditorToPlayerMessageType.StopSession:
                case EditorToPlayerMessageType.StopSessionAndTryReloadScene:
                    return true;
            }

            return false;
        }
    }
    
    
    public interface ISerializableTrackable<out V> {
        TrackableId trackableId { get; }
        V Value { get; }
    }

    
    public interface IReceiver {
        void Receive([NotNull] PlayerToEditorMessage data);
    }

    
    [Serializable]
    public struct TrackableChangesData<T> {
        // removed can be replaced with TrackableID[]
        public T[] added, updated, removed;

        public override string ToString() {
            return $"TrackableChangesData<{nameof(T)}>, added: {added.Length}, updated: {updated.Length}, removed: {removed.Length}";
        }
    }
}
