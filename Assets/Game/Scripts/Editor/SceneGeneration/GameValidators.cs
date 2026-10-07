using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using RhythmDojo.Core;
using RhythmDojo.Gameplay;
using RhythmDojo.UI;

namespace RhythmDojo.EditorTools
{
    public static class GameContentValidator
    {
        public static void Validate(GameSettings settings) => settings.Validate();
    }
    public static class GameSceneValidator
    {
        public static void Validate(Scene scene)
        {
            int listeners = 0;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var listener in root.GetComponentsInChildren<AudioListener>())
                    if (listener.isActiveAndEnabled) listeners++;
            if (listeners != 1)
                throw new InvalidOperationException("Each game scene must have exactly one active AudioListener: " + scene.name);

            foreach (var root in scene.GetRootGameObjects())
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                {
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) > 0)
                        throw new InvalidOperationException("Missing script: " + transform.name);
                    if (transform.GetComponent<Collider>() || transform.GetComponent<Rigidbody>())
                        throw new InvalidOperationException("Gameplay presentation must have no physics: " + transform.name);
                }
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var mainMenu in root.GetComponentsInChildren<MainMenuCompositionRoot>(true)) mainMenu.Validate();
                foreach (var bootstrap in root.GetComponentsInChildren<Bootstrap>(true)) bootstrap.Validate();
                foreach (var composition in root.GetComponentsInChildren<GameplayCompositionRoot>(true)) composition.Validate(true);
                foreach (var screenRoot in root.GetComponentsInChildren<ScreenCompositionRoot>(true)) screenRoot.Validate();
                foreach (var selection in root.GetComponentsInChildren<SongSelectionScreen>(true)) selection.Validate();
                foreach (var settings in root.GetComponentsInChildren<SettingsScreen>(true)) settings.Validate();
            }
        }
    }
}
