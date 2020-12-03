#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ARFoundationRemote.Editor;
using ARFoundationRemote.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;
using Debug = UnityEngine.Debug;


namespace ARFoundationRemote.RuntimeEditor {
    [InitializeOnLoad]
    public class Receiver: MonoBehaviour {
        public ARemoteReceiverState state;

        static List<IReceiver> receivers { get; } = new List<IReceiver>();

        static Receiver instance;
        
        static Receiver Instance {
            get {
                CreateInstanceIfNull();
                return instance;
            }
        }

        static void CreateInstanceIfNull() {
            if (instance == null) {
                Assert.IsFalse(isQuitting, "CreateInstanceIfNull() was called while isQuitting");
                instance = FindObjectOfType<Receiver>();
                if (instance == null) {
                    var type = typeof(Receiver);
                    var go = new GameObject {
                        name = type.Namespace + "." + type.Name,
                        tag = "EditorOnly"
                    };
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<Receiver>();
                }
            }
        }

        // todo this is called on Unity launch. Rewrite to iniOnLoad?
        static Receiver() {
            SessionSubsystem.SetOnSessionStartDelegate(CreateInstanceIfNull);
            
            // XRMeshSubsystemRemote.SetDelegate before ARMeshManager.OnEnable()
            Assert.IsFalse(receivers.OfType<MeshSubsystemReceiver>().Any());
            var meshReceiver = new MeshSubsystemReceiver();
            XRMeshSubsystemRemote.SetDelegate(meshReceiver);
            receivers.Add(meshReceiver);
        }

        void Awake() {
            Assert.IsFalse(isQuitting);
            Assert.AreEqual("EditorOnly", gameObject.tag);
            addSubsystemToReceivers<XRPlaneSubsystem>();
            receivers.Add(new CameraPoseReceiver());
            addSubsystemToReceivers<XRDepthSubsystem>();
            var xrManagerSettings = XRGeneralSettings.Instance.Manager;
            if (!xrManagerSettings.isInitializationComplete) {
                xrManagerSettings.InitializeLoaderSync();
            }
            addSubsystemToReceivers<XRFaceSubsystem>();
            if (TouchInputReceiver.Instance == null) {
                var touchInputReceiver = new GameObject(nameof(TouchInputReceiver)).AddComponent<TouchInputReceiver>();
                DontDestroyOnLoad(touchInputReceiver.gameObject);
                receivers.Add(touchInputReceiver);
            }
            addSubsystemToReceivers<XRImageTrackingSubsystem>();
            addSubsystemToReceivers<XRCameraSubsystem>();
            createAndAddReceiver<OriginDataSender>();
            addSubsystemToReceivers<XRDepthSubsystem>();
            addSubsystemToReceivers<XRSessionSubsystem>();
            #if ARFOUNDATION_4_0_OR_NEWER
                addSubsystemToReceivers<XROcclusionSubsystem>();
                addSubsystemToReceivers<XRHumanBodySubsystem>();
                addSubsystemToReceivers<XRObjectTrackingSubsystem>();
            #endif
            Connection.receiverConnection.Register(playerMessageReceived);
            Connection.receiverConnection.RegisterDisconnection(onPlayerDisconnected);
            StartCoroutine(initCor());

            if (FindObjectOfType<ARRaycastManager>()) {
                if (FindObjectOfType<ARPlaneManager>() == null && FindObjectOfType<ARPointCloudManager>() == null) {
                    Debug.LogWarning("ARRaycastManager found in scene but no ARPlaneManager or ARPointCloudManager is present.");
                    Debug.LogWarning("Please add ARPlaneManager to enable raycast against detected planes.");
                    Debug.LogWarning("Please add ARPointCloudManager to enable raycast against detected cloud points.");
                }
            }
        }

        void Update() {
            foreach (var _ in receivers.OfType<IOnUpdate>()) {
                _.OnUpdate();
            }
        }

        void addSubsystemToReceivers<T>() where T : class, ISubsystem {
            var activeLoader = XRGeneralSettings.Instance.Manager.activeLoader;
            Assert.IsNotNull(activeLoader);
            var subsystem = activeLoader.GetLoadedSubsystem<T>();
            Assert.IsNotNull(subsystem);
            var receiver = subsystem as IReceiver;
            Assert.IsNotNull(receiver);
            receivers.Add(receiver);
        }
       
        void OnApplicationQuit() {
            logDestruction("OnApplicationQuit");
            isQuitting = true;
        }

        [Conditional("_")]
        public static void logDestruction(string s) {
            Debug.Log(s);
        }
        
        void createAndAddReceiver<T>() where T : Component, IReceiver {
            var existing = FindObjectOfType<T>();
            Assert.IsNull(existing);
            if (existing != null) {
                receivers.Add(existing);
            } else {
                var receiver = gameObject.AddComponent<T>();
                receivers.Add(receiver);
            }
        }

        IEnumerator initCor() {
            setState(ARemoteReceiverState.WaitingForConnectedPlayer);
            while (!Connection.receiverConnection.isConnected) {
                yield return null;
            }

            setState(ARemoteReceiverState.WaitingForPlayerResponse);
            new EditorToPlayerMessage {settings = Settings.Instance.arCompanionSettings}.Send();
            SendMessageToRemote(EditorToPlayerMessageType.Init);

            while (Connection.receiverConnection.Connecting) {
                yield return null;
            }
            
            if (!Connection.receiverConnection.isConnected) {
                Debug.LogError(Constants.packageName + ": please ensure that your AR device is unlocked and running ARCompanion app.");
            }
        }

        public static bool isQuitting { get; private set; }
        public static bool HasInstance => !isQuitting && instance != null;

        void OnDestroy() {
            logDestruction($"{nameof(Receiver)} OnDestroy()");
            Connection.receiverConnection.UnregisterDisconnection(onPlayerDisconnected);
        }

        static void SendMessageToRemote(EditorToPlayerMessageType messageType) {
            new EditorToPlayerMessage {messageType = messageType}.Send();
        }

        void onPlayerDisconnected(int _) {
            if (state >= ARemoteReceiverState.WaitingForPlayerResponse) {
                Debug.LogError($"{Constants.packageName}: Editor lost connection with AR Companion app. Please restart Editor scene.");
            }
        }

        void setState(ARemoteReceiverState _state) {
            //print("setState: " + _state);
            state = _state;
            SessionSubsystem.state = _state;
        }

        void playerMessageReceived(PlayerToEditorMessage data) {
            if (data.messageType == PlayerToEditorMessageType.SessionReady) {
                setState(ARemoteReceiverState.Running);
                ReviewRequest.RecordUsage();
            }

            foreach (var receiver in receivers) {
                receiver.Receive(data);
            }
        }
    }

    public interface IOnUpdate {
        void OnUpdate();
    }
}
#endif
