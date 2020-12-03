﻿using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
#if UNITY_2019_2
    using Input = ARFoundationRemote.Input;
#else
    using Input = UnityEngine.Input;
#endif


namespace ARFoundationRemoteExample.Runtime {
    public class MultiTouchRaycastExample : MonoBehaviour {
        [SerializeField] ARRaycastManager raycastManager = null;
        [SerializeField] ARSessionOrigin origin = null;
        [CanBeNull] [SerializeField] GameObject optionalPointerPrefab = null;
        [SerializeField] bool disablePointersOnTouchEnd = false;
        [SerializeField] TrackableType trackableTypeMask = TrackableType.All;

        readonly Dictionary<int, Transform> pointers = new Dictionary<int, Transform>(); 


        void Update() {
            for (int i = 0; i < Input.touchCount; i++) {    
                var touch = Input.GetTouch(i);
                var pointer = getPointer(touch.fingerId);
                var touchPhase = touch.phase;
                if (touchPhase == TouchPhase.Ended || touchPhase == TouchPhase.Canceled) {
                    if (disablePointersOnTouchEnd) {
                        pointer.gameObject.SetActive(false);
                    }
                } else {
                    var ray = origin.camera.ScreenPointToRay(touch.position);
                    var hits = new List<ARRaycastHit>();
                    var hasHit = raycastManager.Raycast(ray, hits, trackableTypeMask);
                    if (hasHit) {
                        var pose = hits.First().pose;
                        pointer.position = pose.position;
                        pointer.rotation = pose.rotation;
                    }
                    
                    pointer.gameObject.SetActive(hasHit);
                }
            }
        }

        Transform getPointer(int fingerId) {
            if (pointers.TryGetValue(fingerId, out var existing)) {
                return existing;
            } else {
                var newPointer = createNewPointer();
                pointers[fingerId] = newPointer;
                return newPointer;
            }
        }
        
        Transform createNewPointer() {
            var result = instantiatePointer();
            result.parent = transform; 
            return result;
        }

        Transform instantiatePointer() {
            if (optionalPointerPrefab != null) {
                return Instantiate(optionalPointerPrefab).transform;
            } else {
                var result = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
                result.localScale = Vector3.one * 0.05f;
                return result;
            }
        }
    }
}
