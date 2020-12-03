using System;
using UnityEngine;
using UnityEngine.Assertions;


namespace ARFoundationRemote.Runtime {
    public class Settings : ScriptableObject {
        public const string defaultCompanionAppIp = "192.168.0.";
        public const string maxFPSTooltip = "This sets only the upper bound. Actual FPS depend on the performance of your AR device.";
        
        static Settings instance;
        
        public static Settings Instance {
            get {
                if (instance == null) {
                    loadFromResources();
                    Assert.IsNotNull(instance);
                }
                
                Assert.IsNotNull(instance);
                return instance;
            }
        }

        static void loadFromResources() {
            instance = Resources.Load<Settings>(nameof(Settings));
        }

        [Header("Connection Settings")]
        [SerializeField] public string ARCompanionAppIP = defaultCompanionAppIp;
        [SerializeField] public int port = 44819;

        [Header("AR Companion Settings")] 
        [Tooltip("Please restart AR scene in Editor to apply settings. Building new AR Companion app is not required.")]
        [SerializeField]
        public ARCompanionSettings arCompanionSettings;

        [SerializeField] 
        public DebugSettings debugSettings;
        
        public InputSimulationType inputSimulationType => InputSimulationType.SimulateSingleTouchWithMouse;

        public bool logStartupErrors => debugSettings.logStartupErrors;
        public bool showTelepathyLogs => debugSettings.showTelepathyLogs;
        public bool showTelepathyWarningsAndErrors => debugSettings.showTelepathyWarningsAndErrors;

        public static bool EnableBackgroundVideo => cameraVideoSettings.enableVideo;
        public static CameraVideoSettings cameraVideoSettings => Instance.arCompanionSettings.cameraVideoSettings;
        public static OcclusionSettings occlusionSettings => Instance.arCompanionSettings.occlusionSettings;
        public static FaceTrackingSettings faceTrackingSettings => Instance.arCompanionSettings.faceTrackingSettings;

        public static DebugSettingsRuntime debugSettingsRuntime => Instance.arCompanionSettings.debugSettings;
    }


    [Serializable]
    public class ARCompanionSettings {
        [HideInInspector] public int maxOutgoingMessages = 2;
        public CameraVideoSettings cameraVideoSettings;
        public OcclusionSettings occlusionSettings;
        public MeshingSettings meshingSettings;
        [Tooltip("Disable unnecessary face tracking features to increase FPS.")]
        [SerializeField] public FaceTrackingSettings faceTrackingSettings;
        [SerializeField] public DebugSettingsRuntime debugSettings;
    }


    [Serializable]
    public struct DebugSettingsRuntime {
        public bool canResetSession => true;
    }


    [Serializable]
    public class FaceTrackingSettings {
        [Tooltip(Settings.maxFPSTooltip)]
        [SerializeField] public float maxFPS = 30;
        [SerializeField] public bool sendVertices = true;
        [SerializeField] public bool sendNormals = true;
        [SerializeField] public bool sendARKitBlendshapes = true;
        [SerializeField] public bool showFacesInCompanionApp = true;
    }
    
    
    [Serializable]
    public class MeshingSettings {
        [SerializeField]
        [Tooltip("Meshes is the Editor are the correct ones. The companion app meshes may not be correct. This option is only for debug purposes.")]
        public bool showMeshesInCompanionApp = false;
    }
    

    [Serializable]
    public class CameraVideoSettings {
        [SerializeField] public bool enableVideo = true;
        [SerializeField] [Range(.01f, 1f)] public float resolutionScale = 1f/3;
        [SerializeField] public int quality = 95;
        [Tooltip(Settings.maxFPSTooltip)]
        [SerializeField] public float maxFPS = 15;
    }


    [Serializable]
    public class OcclusionSettings {
        [Tooltip(Settings.maxFPSTooltip)]
        [SerializeField] public float maxFPS = 10f;
        /// setting scale to 1 will clip the texture, don't know why
        /// also, this may cause companion app crashes
        [SerializeField] [Range(.01f, 0.95f)] public float resolutionScale = 1f/3;
    }
    
    
    public enum InputSimulationType {
        SimulateSingleTouchWithMouseLegacy,
        SimulateSingleTouchWithMouse,
        SimulateMouseWithTouches
    }


    [Serializable]
    public class DebugSettings {
        [SerializeField] public bool logStartupErrors = true;
        [SerializeField] public bool showTelepathyLogs = false;
        [SerializeField] public bool showTelepathyWarningsAndErrors = true;
        [SerializeField] public bool printCompanionAppIPsToConsole = true;
    }
}
