using ARFoundationRemote.Runtime;


public static class EditorToPlayerMessageExtensions {
    public static void Send(this EditorToPlayerMessage msg) {
        #if UNITY_EDITOR
            Connection.receiverConnection.Send(msg);
        #endif
    }
}
