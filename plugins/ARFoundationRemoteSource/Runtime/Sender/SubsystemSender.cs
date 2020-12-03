using JetBrains.Annotations;
using UnityEngine;


// todo rename file
namespace ARFoundationRemote.Runtime {
    public interface ISubsystemSender {
        void EditorMessageReceived([NotNull] EditorToPlayerMessage data);
    }

    public interface ISubsystemSenderUpdateable : ISubsystemSender {
        void UpdateSender();
    }

    public abstract class SubsystemSender: MonoBehaviour, ISubsystemSender {
        public abstract void EditorMessageReceived(EditorToPlayerMessage data);
    }
}
