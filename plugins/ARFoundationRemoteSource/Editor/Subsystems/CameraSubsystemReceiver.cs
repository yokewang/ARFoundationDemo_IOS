using ARFoundationRemote.Runtime;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Assertions;


namespace ARFoundationRemote.Editor {
    public partial class CameraSubsystem : IReceiver {
        [CanBeNull] static ARCameraFrameEventArgsSerializable? receivedCameraFrame { get; set; }
        [CanBeNull] static TextureAndDescriptor[] textures { get; set; }


        void IReceiver.Receive(PlayerToEditorMessage data) {
            var maybeRemoteFrame = data.cameraData?.cameraFrame;
            if (maybeRemoteFrame.HasValue) {
                var remoteFrame = maybeRemoteFrame.Value;
                receivedCameraFrame = remoteFrame;

                var receivedTextures = remoteFrame.textures;
                var count = receivedTextures.Length;
                if (textures == null) {
                    textures = new TextureAndDescriptor[count];
                    for (int i = 0; i < count; i++) {
                        textures[i] = new TextureAndDescriptor();
                    }
                }

                Assert.AreEqual(receivedTextures.Length, textures.Length);
                for (int i = 0; i < count; i++) {
                    textures[i].Update(receivedTextures[i]);
                }
            }
        }
    }
}
