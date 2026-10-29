using UnityEditor;
using UnityEngine;

namespace BerTaDev.DiscordRichPresence.Editor
{
    [CustomEditor(typeof(DiscordPresenceManager))]
    sealed class DiscordPresenceManagerEditor : UnityEditor.Editor
    {
        const string PortalUrl = "https://discord.com/developers/applications";

        public override bool RequiresConstantRepaint() => Application.isPlaying;

        public override void OnInspectorGUI()
        {
            string applicationId = serializedObject.FindProperty("applicationId").stringValue?.Trim();

            if (string.IsNullOrEmpty(applicationId))
            {
                EditorGUILayout.HelpBox("Create an application in the Discord Developer Portal and paste its Application ID below. Upload images under Rich Presence > Art Assets and use their names as image keys.", MessageType.Info);
                if (GUILayout.Button("Open Discord Developer Portal")) Application.OpenURL(PortalUrl);
            }
            else if (!DiscordPresence.IsValidApplicationId(applicationId))
            {
                EditorGUILayout.HelpBox("The Application ID should be a 17-20 digit number (General Information > Application ID).", MessageType.Error);
            }

            if (Application.isPlaying) DrawStatus();

            EditorGUILayout.Space(2);
            DrawDefaultInspector();
        }

        static void DrawStatus()
        {
            if (!DiscordPresence.IsSupported)
                EditorGUILayout.HelpBox("Discord Rich Presence is not available on this platform.", MessageType.Warning);
            else if (DiscordPresence.IsConnected)
                EditorGUILayout.HelpBox("Connected to Discord as " + DiscordPresence.User?.DisplayName + ".", MessageType.None);
            else if (DiscordPresence.IsInitialized)
                EditorGUILayout.HelpBox("Waiting for Discord... Make sure the Discord desktop app is running.", MessageType.None);
            else
                EditorGUILayout.HelpBox("Not initialized. Check the Console for errors.", MessageType.Warning);
        }
    }

    static class DiscordPresenceMenu
    {
        [MenuItem("GameObject/Discord/Discord Presence Manager", false, 10)]
        static void CreateManager(MenuCommand command) => Create<DiscordPresenceManager>("Discord Presence Manager", command);

        [MenuItem("GameObject/Discord/Discord Scene Presence", false, 11)]
        static void CreateScenePresence(MenuCommand command) => Create<DiscordScenePresence>("Discord Scene Presence", command);

        static void Create<T>(string name, MenuCommand command) where T : Component
        {
            var go = new GameObject(name, typeof(T));
            GameObjectUtility.SetParentAndAlign(go, command.context as GameObject);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            Selection.activeObject = go;
        }
    }
}
