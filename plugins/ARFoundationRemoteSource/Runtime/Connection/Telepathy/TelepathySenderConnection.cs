using System.Collections;
using System.Threading;
using UnityEngine;
using Telepathy;


namespace ARFoundationRemote.Runtime {
    public class TelepathySenderConnection : TelepathyConnection<EditorToPlayerMessage, PlayerToEditorMessage> {
        readonly Server server = new Server();


        public static TelepathySenderConnection Create() {
            var gameObject = new GameObject {name = nameof(TelepathySenderConnection)};
            DontDestroyOnLoad(gameObject);
            return gameObject.AddComponent<TelepathySenderConnection>();
        }

        protected override void awake() {
            server.MaxMessageSize = maxMessageSize;
            server.Start(port);
        }

        IEnumerator Start() {
            while (true) {
                yield return new WaitForSeconds(1);
                if (isActive) {
                    yield break;
                }
                
                awake();
            }
        }
        
        protected override Common getCommon() {
            return server;
        }

        protected override bool isConnected_internal => Interlocked.CompareExchange(ref connectionId, 0, 0) != -1;
        
        protected override void send(byte[] payload) {
            if (connectionId != -1) {
                server.Send(connectionId, payload);
            }
        }

        public bool isActive => server.Active;
    }
}
