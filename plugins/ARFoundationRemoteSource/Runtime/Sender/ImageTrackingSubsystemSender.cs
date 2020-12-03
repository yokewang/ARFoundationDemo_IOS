using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;


namespace ARFoundationRemote.Runtime {
    [SuppressMessage("ReSharper", "Unity.PerformanceCriticalCodeInvocation")]
    public class ImageTrackingSubsystemSender : SubsystemSender {
        [SerializeField] ARTrackedImageManager manager = null;
        
        readonly Dictionary<Guid, Guid> guids = new Dictionary<Guid, Guid>();
        readonly List<Texture2D> textures = new List<Texture2D>();

        
        void Awake() {
            Assert.IsNull(manager.referenceLibrary);
            manager.trackedImagesChanged += trackedImagesChanged;
        }

        void OnDestroy() {
            reset();
            manager.trackedImagesChanged -= trackedImagesChanged;
        }

        void trackedImagesChanged(ARTrackedImagesChangedEventArgs args) {
            XRTrackedImageSerializable[] toSerializable(IEnumerable<ARTrackedImage> arTrackedImages) {
                return arTrackedImages.Select(image => XRTrackedImageSerializable.Create(image, getEditorImageGuid(image.referenceImage.guid))).ToArray();
            }
            
            var payload = new TrackableChangesData<XRTrackedImageSerializable> {
                added = toSerializable(args.added),
                updated = toSerializable(args.updated),
                removed = toSerializable(args.removed)
            };
            new PlayerToEditorMessage { trackedImagesData = payload }.Send();
        }

        public override void EditorMessageReceived(EditorToPlayerMessage data) {
            var messageType = data.messageType;
            if (messageType.IsStop()) {
                reset();
                return;
            }
            
            var enableImageTracking = data.enableImageTracking;
            if (enableImageTracking.HasValue) {
                var isEnabled = enableImageTracking.Value;
                if (shouldWait()) {
                    log($"isManagerEnabled = {isEnabled}");
                    isManagerEnabled = isEnabled;
                } else {
                    setManagerEnabled(isEnabled);
                }
            }
            
            trySetNewImageLibrary(data);
            tryAddImage(data);
        }

        void setManagerEnabled(bool isEnabled) {
            log($"setManagerEnabled {isEnabled}");
            isManagerEnabled = isEnabled;
            Sender.Instance.SetManagerEnabled(manager, isEnabled);
        }

        void trySetNewImageLibrary([NotNull] EditorToPlayerMessage data) {
            var imageLibraryContainer = data.imageLibrary;
            if (imageLibraryContainer == null) {
                return;
            }

            log("StartCoroutine(setNewImageLibraryCor(imageLibraryContainer));");
            DontDestroyOnLoadSingleton.AddCoroutine(setNewImageLibraryCor(imageLibraryContainer), nameof(setNewImageLibraryCor));
        }

        void tryAddImage([NotNull] EditorToPlayerMessage data) {
            var imageToAdd = data.imageToAdd;
            if (imageToAdd == null) {
                return;
            }

            log("StartCoroutine(addImage(imageToAdd.Deserialize()));");
            DontDestroyOnLoadSingleton.AddCoroutine(addImage(imageToAdd.Deserialize()), nameof(addImage));
        }

        void reset() {
            log("reset()");
            guids.Clear();
            StopAllCoroutines();
            manager.enabled = false;
            manager.referenceLibrary = null;
            
            textures.ForEach(Destroy);
            textures.Clear();
        }

        IEnumerator setNewImageLibraryCor([NotNull] ImageLibrarySerializableContainer imageLibraryContainer) {
            var serializedLibrary = imageLibraryContainer.library;
            if (serializedLibrary == null) {
                log("receive image library NULL");
                reset();
                yield break;
            }

            while (Defines.isAndroid && ARSession.state < ARSessionState.SessionInitializing) {
                // if we don't wait here, only the first image will be added, don't know why
                log("waiting, ARSession.state < ARSessionState.SessionInitializing");
                yield return null;
            }
            
            while (shouldWait()) {
                yield return null;
            }
  
            log("receive image library, count: " + serializedLibrary.count);
            manager.enabled = false;
            manager.referenceLibrary = null;
            manager.referenceLibrary = manager.CreateRuntimeLibrary();
            for (int i = 0; i < serializedLibrary.count; i++) {
                var addImageIterator = addImage(serializedLibrary.DeserializeImage(i));
                while (addImageIterator.MoveNext()) {
                    yield return null;
                }
            }

            if (getCurrentImages().Any()) {
                log("all remote images: " + string.Join(", ", getCurrentImages().Select(_ => _.name)));
            }
            
            setManagerEnabled(isManagerEnabled);
        }

        IEnumerable<XRReferenceImage> getCurrentImages() {
            var imageLibrary = manager.referenceLibrary;
            for (int i = 0; i < imageLibrary.count; i++) {
                yield return imageLibrary[i];
            }
        }
        
        bool addingImage;

        bool shouldWait() {
            if (addingImage) {
                log("waiting, addingImage");
                return true;
            }
            
            return false;
        }

        IEnumerator addImage(XRReferenceImage image) {
            while (shouldWait()) {
                yield return null;
            }

            var library = manager.referenceLibrary;
            if (library == null) {
                Debug.LogError("ARTrackedImageManager.referenceLibrary is null");
                yield break;
            }

            if (!(library is MutableRuntimeReferenceImageLibrary mutableLibrary)) {
                Debug.LogError("this platform does not support adding reference images at runtime");
                yield break;
            }

            addingImage = true;
            var editorImageGuid = image.guid;
            if (!guids.Values.Contains(editorImageGuid)) {
                var oldGuids = getGuids(mutableLibrary).ToArray();
                var handle = mutableLibrary.ScheduleAddImageJob(image.texture, image.name, image.width);
                log("ScheduleAddImageJob " + image.name + " " + editorImageGuid + " " + image.textureGuid);
                while (!handle.IsCompleted) {
                    yield return null;
                }

                var addedGuids = getGuids(mutableLibrary).Where(_ => !oldGuids.Contains(_)).ToArray();
                if (addedGuids.Length == 1) {
                    var addedGuid = addedGuids.First();
                    log("addedGuid " + addedGuid);
                    guids[addedGuid] = editorImageGuid;                    
                } else {
                    Debug.LogError($"ScheduleAddImageJob failed. Did you add duplicate images?\nGuids: {addedGuids.Length}, image: {image.name}");
                }
            } else {
                log("image already added " + image.name);
            }
            
            textures.Add(image.texture);
            addingImage = false;
        }

        /// When we ScheduleAddImageJob to MutableRuntimeReferenceImageLibrary, a new guid will be generated for image.
        /// We save original guid to send it send back to Editor.
        Guid getEditorImageGuid(Guid guid) {
            if (guids.TryGetValue(guid, out var result)) {
                return result;
            } else {
                if (!loggedNotFoundGuids.Contains(guid)) {
                    loggedNotFoundGuids.Add(guid);
                    log("editor image guid not found " + guid);
                }
                
                return guid;
            }
        }

        readonly HashSet<Guid> loggedNotFoundGuids = new HashSet<Guid>();
        bool isManagerEnabled;

        static IEnumerable<Guid> getGuids(MutableRuntimeReferenceImageLibrary mutableLibrary) {
            return toEnumerable(mutableLibrary).Select(_ => _.guid);
        }

        static IEnumerable<XRReferenceImage> toEnumerable(MutableRuntimeReferenceImageLibrary mutableLibrary) {
            foreach (var image in mutableLibrary) {
                yield return image;
            }
        }
    
        [Conditional("_")]
        static void log(string s) {
            Debug.Log(nameof(ImageTrackingSubsystemSender) + ": " + s);
        }
    }


    [Serializable]
    public class XRTrackedImageSerializable: ISerializableTrackable<XRTrackedImage> {
        TrackableIdSerializable trackableIdSer;
        Guid sourceImageId;
        PoseSerializable pose;
        Vector2Serializable size;
        TrackingState trackingState;
        IntPtr nativePtr;


        public static XRTrackedImageSerializable Create(ARTrackedImage i, Guid guid) {
            return new XRTrackedImageSerializable {
                trackableIdSer = TrackableIdSerializable.Create(i.trackableId),
                sourceImageId = guid,
                pose = PoseSerializable.Create(i.transform.LocalPose()),
                size = Vector2Serializable.Create(i.size),
                trackingState = i.trackingState,
                nativePtr = i.nativePtr
            };
        }
        
        public TrackableId trackableId => trackableIdSer.Value;
        public XRTrackedImage Value => new XRTrackedImage(trackableId, sourceImageId, pose.Value, size.Value, trackingState, nativePtr);
    }


    [Serializable]
    public class ImageLibrarySerializableContainer {
        [CanBeNull] public ImageLibrarySerializable library;
    }
    
    
    [Serializable]
    public class ImageLibrarySerializable {
        readonly List<XRReferenceImageSerializable> images = new List<XRReferenceImageSerializable>();


        public ImageLibrarySerializable([CanBeNull] IReferenceImageLibrary library, Func<Guid, Texture2D> loadTextureFromDatabase) {
            if (library != null) {
                for (int i = 0; i < library.count; i++) {
                    var referenceImage = library[i];
                    Texture2D textureOverride = null;
                    if (referenceImage.texture == null) {
                        // will be null if Keep Texture at Runtime == false
                        textureOverride = loadTextureFromDatabase(referenceImage.textureGuid);
                    }
                    
                    images.Add(XRReferenceImageSerializable.Create(referenceImage, textureOverride));
                }
            }
        }

        public int count => images.Count;
        
        public XRReferenceImage DeserializeImage(int index) => images[index].Deserialize(); 
    }

    
    [Serializable]
    public class XRReferenceImageSerializable {
        static readonly FieldInfo
            m_SerializedGuid = getField("m_SerializedGuid"),
            m_SerializedTextureGuid = getField("m_SerializedTextureGuid");
        
        public SerializableGuid guid;
        public SerializableGuid textureGuid;
        public Vector2Serializable size;
        public string name;
        public byte[] textureBytes;


        public XRReferenceImage Deserialize() {
            var sizeValue = size.Value;
            var tex = new Texture2D(Mathf.RoundToInt(sizeValue.x), Mathf.RoundToInt(sizeValue.y));
            #if AR_FOUNDATION_REMOTE_INSTALLED
                var loaded = tex.LoadImage(textureBytes);
                Assert.IsTrue(loaded);
            #endif
            return new XRReferenceImage(guid, textureGuid, sizeValue, name, tex);
        }

        public static XRReferenceImageSerializable Create(XRReferenceImage i, [CanBeNull] Texture2D textureOverride) {
            var result = new XRReferenceImageSerializable {
                guid = getGuid(i, m_SerializedGuid),
                textureGuid = getGuid(i, m_SerializedTextureGuid),
                size = Vector2Serializable.Create(i.size),
                name = i.name,
                textureBytes = encodeToPng(textureOverride != null ? textureOverride : i.texture)
            };
            // ImageTrackingSubsystemSender.logIfNeeded("XRReferenceImageSerializable created " + result.name + " " + result.guid);
            return result;
        }

        static byte[] encodeToPng([NotNull] Texture2D tex) {
            Assert.IsNotNull(tex);
            
            var width = tex.width;
            var height = tex.height;
            
            var rt = RenderTexture.GetTemporary(width, height);
            RenderTexture.active = rt;
            Graphics.Blit(tex, rt);
            var targetImage = new Texture2D(width, height);
            targetImage.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            targetImage.Apply();
            RenderTexture.active = null;
            #if AR_FOUNDATION_REMOTE_INSTALLED
                var result = targetImage.EncodeToPNG();
                Object.Destroy(targetImage);
                RenderTexture.ReleaseTemporary(rt);
                return result;
            #else
                throw new Exception();
            #endif
        }
        
        static SerializableGuid getGuid(XRReferenceImage i, FieldInfo field) {
            return (SerializableGuid) field.GetValue(i);
        }

        static FieldInfo getField(string name) {
            var result = typeof(XRReferenceImage)
                .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.GetField);
            Assert.IsNotNull(result);
            return result;
        }
    }
}
