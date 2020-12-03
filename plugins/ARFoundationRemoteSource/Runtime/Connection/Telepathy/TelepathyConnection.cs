﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Events;
using Telepathy;
using UnityEngine.Assertions;
using Debug = UnityEngine.Debug;
using EventType = Telepathy.EventType;
using ThreadPriority = System.Threading.ThreadPriority;


namespace ARFoundationRemote.Runtime {
    public abstract class TelepathyConnection<IncomingMessageType, OutgoingMessageType> : MonoBehaviour, IConnection<IncomingMessageType, OutgoingMessageType> where IncomingMessageType : class {
        public bool CanSendNonCriticalMessage => outgoingMessages.Count < Settings.Instance.arCompanionSettings.maxOutgoingMessages;

        
        protected const int maxMessageSize = 100 * 1024 * 1024;
        protected static int port => Settings.Instance.port;
        [CanBeNull] Action<IncomingMessageType> messageCallback;
        readonly HashSet<UnityAction<int>> disconnectionCallbacks = new HashSet<UnityAction<int>>();

        protected int connectionId = -1;
        Thread backgroundThread;
        protected readonly ConcurrentQueue<IncomingMessage> incomingMessages = new ConcurrentQueue<IncomingMessage>();
        readonly ConcurrentQueue<OutgoingMessageType> outgoingMessages = new ConcurrentQueue<OutgoingMessageType>();
        
        
        void Awake() {
            log($"{GetType().Name} {nameof(Awake)}()");
            if (Settings.Instance.showTelepathyLogs) {
                Telepathy.Logger.Log = Debug.Log;
            }

            if (Settings.Instance.showTelepathyWarningsAndErrors) {
                Telepathy.Logger.LogWarning = msg => {
                    if (!Application.isEditor) {
                        Sender.waitingErrorMessage += msg + "\n";
                    }

                    Debug.LogWarning(msg);
                };
                
                Telepathy.Logger.LogError = msg => {
                    Sender.waitingErrorMessage += msg + "\n";
                    Debug.LogError(msg);
                };
            }
            
            awake();

            backgroundThread = new Thread(runBackgroundThread) {IsBackground = true, Priority = ThreadPriority.Highest};
            backgroundThread.Start();
        }

        [Conditional("_")]
        void log(string s) {
            Debug.Log(s);
        }

        protected abstract void awake();

        void OnDestroy() {
            backgroundThread.Abort(); // Interrupt() causes delays on the next connection
            onDestroyInternal();
        }

        protected virtual void onDestroyInternal() {
        }

        void runBackgroundThread() {
            try {
                while (true) {
                    while (getCommon().GetNextMessage(out var msg)) {
                        var id = msg.connectionId;
                        var eventType = msg.eventType;
                        switch (eventType) {
                            case EventType.Connected:
                                connectionId = id;
                                break;
                            case EventType.Data:
                                break;
                            case EventType.Disconnected:
                                if (connectionId == id) {
                                    connectionId = -1;
                                }

                                break;
                            default:
                                throw new Exception();
                        }

                        // pass all messages to main thread
                        incomingMessages.Enqueue(new IncomingMessage {
                            message = eventType == EventType.Data ? msg.data.Deserialize<IncomingMessageType>() : null,
                            connectionId = id,
                            eventType = eventType
                        });
                    }

                    if (isConnected) {
                        while (outgoingMessages.TryDequeue(out var msg)) {
                            send(msg.SerializeToByteArray());
                        }
                    }
                }
            } catch (ThreadAbortException) {
            }
        }

        void Update() {
            while (incomingMessages.TryDequeue(out var msg)) {
                switch (msg.eventType) {
                    case EventType.Connected: {
                        break;
                    }
                    case EventType.Data: {
                        if (messageCallback != null) {
                            messageCallback(msg.message);
                        }
                        break;
                    }
                    case EventType.Disconnected: {
                        foreach (var disconnectionCallback in disconnectionCallbacks) {
                            disconnectionCallback(msg.connectionId);
                        }

                        break;
                    }
                    default:
                        throw new Exception();
                }
            }
        }

        protected abstract Common getCommon();
        public bool isConnected => isConnected_internal;
        protected abstract bool isConnected_internal { get; }

        public void Register(Action<IncomingMessageType> callback) {
            // Assert.IsNull(messageCallback); // can be not null if we load new scene
            messageCallback = callback;
        }
        
        public void Send(OutgoingMessageType msg) {
            outgoingMessages.Enqueue(msg);
        }

        protected abstract void send(byte[] payload);

        public void RegisterDisconnection(UnityAction<int> callback) {
            Assert.IsFalse(disconnectionCallbacks.Contains(callback));
            disconnectionCallbacks.Add(callback);
        }

        public void UnregisterDisconnection(UnityAction<int> callback) {
            Assert.IsTrue(disconnectionCallbacks.Contains(callback));
            var removed = disconnectionCallbacks.Remove(callback);
            Assert.IsTrue(removed);
        }
        
        protected struct IncomingMessage {
            [CanBeNull] public IncomingMessageType message;
            public int connectionId;
            public EventType eventType;
        }
    }
}
