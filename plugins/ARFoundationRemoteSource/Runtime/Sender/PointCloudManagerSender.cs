﻿using System;
using System.Collections.Generic;
using System.Linq;
using ARFoundationRemote.Runtime;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;


namespace ARFoundationRemote.Runtime {
    [RequireComponent(typeof(ARSessionOrigin))]
    public class PointCloudManagerSender: SubsystemSender {
        [SerializeField] ARPointCloudManager manager = null;
        [SerializeField] ARPointCloudParticleVisualizer pointCloudPrefab = null;
        
        
        void Awake() {
            if (Application.isEditor) {
                Debug.LogError(GetType().Name + " is written for running on device, not in Editor");
            }
            
            manager.pointCloudPrefab = pointCloudPrefab.gameObject;
            manager.pointCloudsChanged += pointCloudsChanged;
        }

        void OnDestroy() {
            manager.pointCloudsChanged -= pointCloudsChanged;
        }

        void pointCloudsChanged(ARPointCloudChangedEventArgs args) {
            var payload = PointCloudData.Create(args);
            //print("send points\n" + payload);
            new PlayerToEditorMessage {pointCloudData = payload}.Send();
        }

        public override void EditorMessageReceived(EditorToPlayerMessage data) {
            var enableDepthSubsystem = data.enableDepthSubsystem;
            if (enableDepthSubsystem.HasValue) {
                Sender.Instance.SetManagerEnabled(manager, enableDepthSubsystem.Value);
            }
        }
    }
}


[Serializable]
public class PointCloudData {
    public ARPointCloudSerializable[] added, updated, removed;

    public static PointCloudData Create(ARPointCloudChangedEventArgs args) {
        return new PointCloudData {
            added = toSerializable(args.added),
            updated = toSerializable(args.updated),
            removed = toSerializable(args.removed)
        };
    }

    static ARPointCloudSerializable[] toSerializable(List<ARPointCloud> points) {
        return points.Select(ARPointCloudSerializable.Create).ToArray();
    }

    public override string ToString() {
        string result = "";
        if (added.Any()) {
            result += "added:\n";
            foreach (var p in added) {
                result += p.trackableId + "\n";
            }
        }

        if (updated.Any()) {
            result += "updated: " + updated.Length + "\n";                
        }
            
        if (removed.Any()) {
            result += "removed:\n";
            foreach (var p in removed) {
                result += p.trackableId + "\n";
            }
        }
            
        return result;
    }
}


[Serializable]
public class ARPointCloudSerializable: ISerializableTrackable<XRPointCloud> {
    TrackableIdSerializable trackableIdSer;
    PoseSerializable poseSer;
    TrackingState trackingState;
    public Vector3Serializable[] positions { get; private set; }
    public ulong[] identifiers { get; private set; }
    
    public TrackableId trackableId => trackableIdSer.Value;
    
    
    public static ARPointCloudSerializable Create(ARPointCloud c) {
        var positions = c.positions;
        var ids = c.identifiers;
        return new ARPointCloudSerializable {
            trackableIdSer = TrackableIdSerializable.Create(c.trackableId),
            poseSer = PoseSerializable.Create(c.transform.LocalPose()),
            trackingState = c.trackingState,
            positions = positions.HasValue ? positions.Value.Select(Vector3Serializable.Create).ToArray() : new Vector3Serializable[0],
            identifiers = ids.HasValue ? ids.Value.ToArray() : new ulong[0]
        };
    }
    
    public XRPointCloud Value => new XRPointCloud(trackableIdSer.Value, poseSer.Value, trackingState, IntPtr.Zero);
}
