using UnityEditor;
using UnityEngine;
using Sdx17.Multiplayer;

namespace Sdx17.Multiplayer.Editor
{
    /// <summary>
    /// Custom inspector for <see cref="MultiplayerConfig"/>.
    /// Shows the config's own validation result inline, so a bad port or
    /// player limit is visible in the Inspector before anyone presses Play.
    /// </summary>
    [CustomEditor(typeof(MultiplayerConfig))]
    public sealed class MultiplayerConfigEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script");
            serializedObject.ApplyModifiedProperties();

            var config = (MultiplayerConfig)target;
            if (config == null)
            {
                return;
            }

            string problem = config.Validate();
            if (problem != null)
            {
                EditorGUILayout.HelpBox($"Config problem: {problem}", MessageType.Error);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    $"Valid config: {config.ServerAddress}:{config.ServerPort}, up to {config.MaxPlayers} players, " +
                    $"server-authoritative: {(config.ServerAuthoritative ? "on" : "OFF — clients can change their own state, demo use only")}.",
                    config.ServerAuthoritative ? MessageType.Info : MessageType.Warning);
            }

            if (!config.ServerAuthoritative)
            {
                EditorGUILayout.HelpBox(
                    "Server authority is off. Only use this for a local demo. In any real game a client could change its own health and inventory.",
                    MessageType.Warning);
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Open Multiplayer README"))
            {
                Debug.Log("[Sdx17.Multiplayer] See README.md in the package root for setup, the public API and the Play-mode test checklist.");
            }
        }
    }

    /// <summary>
    /// Menu items under Sdx17 > Multiplayer for the one-click setup steps
    /// a new project needs: a config asset and a manager GameObject.
    /// </summary>
    public static class MultiplayerMenu
    {
        private const string ConfigPath = "Assets/MultiplayerConfig.asset";

        [MenuItem("Sdx17/Multiplayer/Create Multiplayer Config", false, 10)]
        public static void CreateMultiplayerConfig()
        {
            var config = ScriptableObject.CreateInstance<MultiplayerConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
            AssetDatabase.SaveAssets();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = config;
            Debug.Log($"[Sdx17.Multiplayer] Created {ConfigPath}. Edit the address, port and player limit, then assign it to a MultiplayerManager.");
        }

        [MenuItem("Sdx17/Multiplayer/Create Multiplayer Manager", false, 11)]
        public static void CreateMultiplayerManager()
        {
            var go = new GameObject("MultiplayerManager");
            go.AddComponent<MultiplayerManager>();
            Undo.RegisterCreatedObjectUndo(go, "Create Multiplayer Manager");
            Selection.activeGameObject = go;
            Debug.Log("[Sdx17.Multiplayer] Created a MultiplayerManager GameObject. Assign a MultiplayerConfig to it, then use Samples~/Demo for a first Play-mode run.");
        }

        [MenuItem("Sdx17/Multiplayer/Log Setup Checklist", false, 20)]
        public static void LogSetupChecklist()
        {
            Debug.Log(
                "[Sdx17.Multiplayer] Setup checklist:\n" +
                "1. Create a Multiplayer Config (this menu) and assign it to the manager.\n" +
                "2. Add the DemoMultiplayer component from Samples~/Demo to a GameObject.\n" +
                "3. Press Play, press H for host or C for client.\n" +
                "4. For real networking, install com.unity.netcode.gameobjects and follow Runtime/Netcode/README.md.");
        }
    }
}
