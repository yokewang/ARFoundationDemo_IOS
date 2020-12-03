#if UNITY_EDITOR
using System;
using System.Collections;
using System.Diagnostics;
using System.Net;
using ARFoundationRemote.Runtime;
using UnityEngine;
using Telepathy;
using UnityEngine.Assertions;
using Debug = UnityEngine.Debug;
using EventType = Telepathy.EventType;


namespace ARFoundationRemote.RuntimeEditor {
    public class TelepathyReceiverConnection : TelepathyConnection<PlayerToEditorMessage, EditorToPlayerMessage> {
        readonly Client client = new Client();

        
        public static TelepathyReceiverConnection Create() {
            var gameObject = new GameObject {name = nameof(TelepathyReceiverConnection)};
            DontDestroyOnLoad(gameObject);
            return gameObject.AddComponent<TelepathyReceiverConnection>(); 
        }

        protected override void awake() {
            var ip = Settings.Instance.ARCompanionAppIP;
            if (!IPAddress.TryParse(ip, out _)) {
                Debug.LogError("Please enter correct AR Companion app IP in Assets/Plugins/ARFoundationRemoteInstaller/Resources/Settings");
                return;
            }
            
            client.MaxMessageSize = maxMessageSize;
            client.Connect(ip, port);
        }

        IEnumerator Start() {
            while (client.Connecting) {
                yield return null;
            }
            
            if (Settings.Instance.logStartupErrors && !isConnected) {
                Debug.LogError("Connection to AR Companion app failed. Please check that app is running and IP is correct in Assets/Plugins/ARFoundationRemoteInstaller/Resources/Settings");
            }
        }

        protected override Common getCommon() {
            return client;
        }

        protected override bool isConnected_internal => client.Connected;

        protected override void send(byte[] payload) {
            Assert.IsTrue(isConnected);
            send_internal(payload);
        }

        void send_internal(byte[] payload) {
            client.Send(payload);
        }

        const double defaultTimeout = 1000;
        
        public PlayerToEditorMessage BlockUntilReceive(EditorToPlayerMessage msg, double timeout = defaultTimeout) {
            Assert.IsFalse(msg.requestGuid.HasValue);
            var guid = Guid.NewGuid();
            msg.requestGuid = guid;
            msg.Send();
            return Connection.receiverConnection.BlockUntilReceive(guid, timeout);
        }

        PlayerToEditorMessage BlockUntilReceive(Guid guid, double timeout = defaultTimeout) {
            if (!isConnected) {
                // prevent Unity freeze when blocking method is called every frame
                throw new Exception($"{Constants.packageName}: please don't call blocking methods while AR Companion is not connected");
            }
            
            var stopwatch = Stopwatch.StartNew();
            while (true) {
                if (stopwatch.Elapsed > TimeSpan.FromMilliseconds(timeout)) {
                    throw new Exception($"{Constants.packageName}: {nameof(BlockUntilReceive)} timeout");
                }

                foreach (var msg in incomingMessages) {
                    if (msg.eventType == EventType.Data) {
                        var playerMessage = msg.message;
                        if (playerMessage.responseGuid == guid) {
                            // Debug.Log($"received, elapsed time: {stopwatch.Elapsed.Milliseconds}");
                            return playerMessage;
                        }
                    }
                }
            }
        }

        protected override void onDestroyInternal() {
            client.Disconnect();
        }
        
        public bool Connecting => client.Connecting;
    }
}
#endif
