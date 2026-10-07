using System;
using UnityEditor;
using UnityEngine;
using RhythmDojo.Audio;
using RhythmDojo.Core;
using RhythmDojo.UI;
using RhythmDojo.Gameplay;
using RhythmDojo.Presentation;

namespace RhythmDojo.EditorTools
{
    public static class SceneDependencyAssembler
    {
        public static void Assemble(SongSelectionView view, GameSettings settings)
        {
            var screen = view.gameObject.AddComponent<SongSelectionScreen>();
            Wire(screen, "view", view);
            var root = view.gameObject.AddComponent<ScreenCompositionRoot>();
            Wire(root, "settings", settings); Wire(root, "selection", screen);
        }
        public static void Assemble(SettingsView view, GameSettings settings)
        {
            var screen = view.gameObject.AddComponent<SettingsScreen>();
            Wire(screen, "view", view);
            var root = view.gameObject.AddComponent<ScreenCompositionRoot>();
            Wire(root, "settings", settings); Wire(root, "audioSettings", screen);
        }
        public static void Wire(UnityEngine.Object target, string property, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(target); var field = serialized.FindProperty(property);
            if (field == null || field.propertyType != SerializedPropertyType.ObjectReference)
                throw new InvalidOperationException($"Missing object field {property} on {target}.");
            field.objectReferenceValue = value; serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        public static void Assemble(GameplaySceneParts parts, GeneratedContent content)
        {
            Wire(parts.Clock, "source", parts.Audio);
            // NewScene can unload prefab components retained by the previous scene. Resolve
            // persistent assets after creating the destination scene, before assigning them.
            Wire(parts.Notes, "tapTemplate", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/TapNote.prefab").GetComponent<NoteView>());
            Wire(parts.Notes, "holdTemplate", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/HoldNote.prefab").GetComponent<NoteView>());
            Wire(parts.Notes, "noteMaterial", content.NoteMaterial);
            Wire(parts.Playfield, "highwayMaterial", content.HighwayMaterial); Wire(parts.Playfield, "railMaterial", content.RailMaterial);
            Wire(parts.Playfield, "judgmentMaterial", content.JudgmentMaterial);
            Wire(parts.Root, "settings", content.Settings); Wire(parts.Root, "controller", parts.Controller);
            Wire(parts.Root, "clock", parts.Clock); Wire(parts.Root, "input", parts.Input); Wire(parts.Root, "notes", parts.Notes);
            Wire(parts.Root, "playfield", parts.Playfield); Wire(parts.Root, "hud", parts.Hud); Wire(parts.Root, "ui", parts.Ui);
            Wire(parts.Root, "hits", parts.Hits);
            Wire(parts.Hud, "view", parts.HudView); Wire(parts.Ui, "view", parts.HudView);
        }
    }
}
