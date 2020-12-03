﻿using System;
using JetBrains.Annotations;
using UnityEngine.Events;


namespace ARFoundationRemote.Runtime {
    public static class Connection {
        [CanBeNull] static TelepathySenderConnection _senderConnection;
        public static TelepathySenderConnection senderConnection {
            get {
                #if UNITY_EDITOR
                throw new Exception("senderConnection getter in Editor!");
                #endif

                #pragma warning disable 162
                if (_senderConnection == null) {
                    _senderConnection = TelepathySenderConnection.Create();
                }

                return _senderConnection;
                #pragma warning restore
            }
        }
        
        #if UNITY_EDITOR
            public static readonly RuntimeEditor.TelepathyReceiverConnection receiverConnection = RuntimeEditor.TelepathyReceiverConnection.Create();
        #endif
    }

    public interface IConnection<IncomingMessageType, OutgoingMessageType>{
        bool isConnected { get; }
        void Register(Action<IncomingMessageType> callback);
        void Send([NotNull] OutgoingMessageType msg);
        void RegisterDisconnection(UnityAction<int> callback);
        void UnregisterDisconnection(UnityAction<int> callback);
    }
}
