using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CocoonPrototype.Editor
{
    /// <summary>Read-only validation of an imported, licensed project checkout.</summary>
    public static class CocoonProjectValidation
    {
        [MenuItem("Cocoon/Validate Project")]
        public static void Validate()
        {
            try
            {
                string[] models = { "ZOOX.obj", "solid.obj", "bag.obj", "seat.obj", "wheelchair.obj", "SeatV2/seat_obj.obj" };
                foreach (string model in models)
                {
                    string path = "Assets/ImportedAssets/CocoonTaxi/" + model;
                    if (!File.Exists(path) || new FileInfo(path).Length < 1024)
                        throw new InvalidOperationException("Missing model or LFS pointer: " + path);
                }
                if (!Directory.Exists("Assets/JapaneseCity"))
                    throw new InvalidOperationException("Install the separately licensed Japanese City package first. See docs/third-party-assets.md.");
                const string scenePath = "Assets/Scenes/Cocoon_OnboardingVR.unity";
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                int missing = 0;
                int machines = 0;
                int objects = 0;
                foreach (var root in scene.GetRootGameObjects())
                {
                    machines += root.GetComponentsInChildren<CocoonTaxiStateMachine>(true).Length;
                    foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                    {
                        objects++;
                        missing += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
                    }
                }
                if (missing != 0) throw new InvalidOperationException("Scene contains missing scripts: " + missing);
                if (machines != 1) throw new InvalidOperationException("Expected one taxi state machine; found " + machines);
                if (!EditorBuildSettings.scenes.Any(s => s.enabled && s.path == scenePath))
                    throw new InvalidOperationException("The authored scene is not enabled in Build Settings.");
                Debug.Log("COCOON_VALIDATION_PASS: scene loaded; " + objects + " objects; one state machine; no missing scripts; model files present. No headset runtime test performed.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                else throw;
            }
        }
    }
}
