using System;
using System.Linq;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using RhythmDojo.Application;
using RhythmDojo.Gameplay;
using RhythmDojo.EditorTools;
using RhythmDojo.Presentation;
using RhythmDojo.Core;
using RhythmDojo.UI;

namespace RhythmDojo.Tests
{
    public sealed class ContentTests
    {
        private GameSettings Settings => AssetDatabase.LoadAssetAtPath<GameSettings>(TestContentBuilder.SettingsPath);
        [Test]
        public void SettingsAndTwoSongsAreValid()
        {
            Settings.Validate();
            Assert.That(Settings.catalog.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(Settings.catalog[0].Chart.Count, Is.EqualTo(18)); Assert.That(Settings.catalog[1].Chart.Count, Is.EqualTo(8));
            Assert.That(Settings.catalog[1].Timing.ToTempoMap().BpmAt(6), Is.EqualTo(180));
        }
        // Preserve exact asset bytes and meta files so migration tests retain original GUIDs.
        private static void WithSettingsRestored(Action test)
        {
            const string folder = "Assets/Game/Settings";
            AssetDatabase.SaveAssets();
            var originals = Directory.GetFiles(folder).ToDictionary(p => p, File.ReadAllBytes);
            try { test(); }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                // Invalidate recreated asset instances before restoring their original GUIDs.
                foreach (var path in Directory.GetFiles(folder).Where(p => p.EndsWith(".asset")))
                    AssetDatabase.DeleteAsset(path.Replace('\\', '/'));
                foreach (var entry in originals) File.WriteAllBytes(entry.Key, entry.Value);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                foreach (var path in originals.Keys.Where(p => p.EndsWith(".asset")))
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            }
        }
        private static void DeleteMigrationTargets()
        {
            foreach (var name in new[] { "Standard", "ScrollSettings", "AudioPlaybackSettings", "TestSong" })
                Assert.That(AssetDatabase.DeleteAsset($"Assets/Game/Settings/{name}.asset"), Is.True);
            AssetDatabase.DeleteAsset("Assets/Game/Settings/SongCatalog.asset");
        }
        private static void SetNumber(UnityEngine.Object target, string field, double value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).doubleValue = value; serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }
        [Test]
        public void FirstCreationMigratesLegacyValuesAndOffset()
        {
            WithSettingsRestored(() =>
            {
                var legacy = AssetDatabase.LoadAssetAtPath<RhythmGameConfig>(SceneBuilder.ConfigPath);
                SetNumber(legacy, "perfectWindow", .06); SetNumber(legacy, "goodWindow", .12);
                SetNumber(legacy, "scrollSpeed", 9); SetNumber(legacy, "scheduleLeadTime", .35); SetNumber(legacy, "audioOffset", .08);
                DeleteMigrationTargets(); AssetDatabase.DeleteAsset(TestContentBuilder.SettingsPath);
                var generated = TestContentBuilder.Prepare().Settings;
                Assert.That(generated.defaultDifficulty.ToSettings().PerfectWindow, Is.EqualTo(.06));
                Assert.That(generated.defaultDifficulty.ToSettings().GoodWindow, Is.EqualTo(.12));
                Assert.That(generated.audio.ScheduleLeadTime, Is.EqualTo(.35));
                Assert.That(generated.scroll.CreateTimeline(ScrollMode.Constant, new SongTiming().ToTempoMap(), 1).DistanceBetween(0,1), Is.EqualTo(9));
                Assert.That(generated.catalog[0].Timing.chartAudioOffsetSeconds, Is.EqualTo(.08));
            });
        }
        [Test]
        public void FreshCreationWithoutLegacyUsesNewDefaults()
        {
            WithSettingsRestored(() =>
            {
                foreach (var path in Directory.GetFiles("Assets/Game/Settings", "*.asset"))
                    AssetDatabase.DeleteAsset(path.Replace('\\', '/'));
                var generated = TestContentBuilder.Prepare().Settings;
                Assert.That(generated.audio.ScheduleLeadTime, Is.EqualTo(.15));
                Assert.That(generated.defaultDifficulty.ToSettings().PerfectWindow, Is.EqualTo(.05));
                Assert.That(generated.catalog[0].Timing.chartAudioOffsetSeconds, Is.Zero);
                Assert.That(File.Exists(SceneBuilder.ConfigPath), Is.False);
            });
        }
        [TestCase(false)] [TestCase(true)]
        public void CompletedMigrationPreservesEasyAndAudioWhenLegacyIsInvalidOrAbsent(bool absent)
        {
            WithSettingsRestored(() =>
            {
                Settings.defaultDifficulty = Settings.difficulties.Single(d => d.DisplayName == "Easy");
                Settings.audio.MigrateLeadTime(.23); EditorUtility.SetDirty(Settings); EditorUtility.SetDirty(Settings.audio);
                string audioBefore = EditorJsonUtility.ToJson(Settings.audio);
                string songBefore = EditorJsonUtility.ToJson(Settings.catalog[0]);
                string guid = AssetDatabase.AssetPathToGUID("Assets/Game/Settings/AudioPlaybackSettings.asset");
                if (absent) AssetDatabase.DeleteAsset(SceneBuilder.ConfigPath);
                else SetNumber(AssetDatabase.LoadAssetAtPath<RhythmGameConfig>(SceneBuilder.ConfigPath), "goodWindow", -1);
                var generated = TestContentBuilder.Prepare().Settings;
                Assert.That(generated.defaultDifficulty.DisplayName, Is.EqualTo("Easy"));
                Assert.That(EditorJsonUtility.ToJson(generated.audio), Is.EqualTo(audioBefore));
                Assert.That(EditorJsonUtility.ToJson(generated.catalog[0]), Is.EqualTo(songBefore));
                Assert.That(AssetDatabase.AssetPathToGUID("Assets/Game/Settings/AudioPlaybackSettings.asset"), Is.EqualTo(guid));
            });
        }
        [Test]
        public void InvalidLegacyFailsOnlyWhenMigrationIsNeeded()
        {
            WithSettingsRestored(() =>
            {
                SetNumber(AssetDatabase.LoadAssetAtPath<RhythmGameConfig>(SceneBuilder.ConfigPath), "goodWindow", -1);
                AssetDatabase.DeleteAsset("Assets/Game/Settings/Standard.asset");
                Assert.Throws<InvalidOperationException>(() => TestContentBuilder.Prepare());
                Assert.That(File.Exists("Assets/Game/Settings/Standard.asset"), Is.False);
            });
        }
        [Test]
        public void GeneratedViewValidationUsesChartKindsAndRuntimeCanRebuildViews()
        {
            var scene = EditorSceneManager.OpenScene(SceneBuilder.GameplayPath);
            try
            {
                var presenter = UnityEngine.Object.FindFirstObjectByType<NotePresenter>();
                var serialized = new SerializedObject(presenter); var views = serialized.FindProperty("views");
                var tap = (NoteView)views.GetArrayElementAtIndex(0).objectReferenceValue; tap.name = "Hold named tap";
                Assert.DoesNotThrow(() => GameSceneValidator.Validate(scene));
                var hold = (NoteView)views.GetArrayElementAtIndex(4).objectReferenceValue; hold.name = "Unnamed note";
                var holdFields = new SerializedObject(hold); holdFields.FindProperty("body").objectReferenceValue = null; holdFields.ApplyModifiedPropertiesWithoutUndo();
                Assert.Throws<InvalidOperationException>(() => GameSceneValidator.Validate(scene));
                Assert.DoesNotThrow(() => UnityEngine.Object.FindFirstObjectByType<GameplayCompositionRoot>().Validate());
                views.ClearArray(); serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.Throws<InvalidOperationException>(() => GameSceneValidator.Validate(scene));
                Assert.DoesNotThrow(() => UnityEngine.Object.FindFirstObjectByType<GameplayCompositionRoot>().Validate());
            }
            finally { EditorSceneManager.OpenScene(SceneBuilder.BootstrapPath); }
        }
        [TestCase("Bootstrap", "settings")]
        [TestCase("SongSelection", "settingsButton")]
        [TestCase("Settings", "bufferList")]
        [TestCase("Settings", "actualState")]
        [TestCase("Settings", "status")]
        [TestCase("Settings", "apply")]
        [TestCase("Settings", "back")]
        [TestCase("Settings", "settings")]
        public void MissingRequiredScreenReferencesFail(string sceneName, string field)
        {
            var scene = EditorSceneManager.OpenScene($"Assets/Game/Scenes/{sceneName}.unity");
            try
            {
                MonoBehaviour screen = sceneName == "Bootstrap" ? UnityEngine.Object.FindFirstObjectByType<Bootstrap>() :
                    field == "settings" ? UnityEngine.Object.FindFirstObjectByType<ScreenCompositionRoot>() :
                    sceneName == "Settings" ? UnityEngine.Object.FindFirstObjectByType<SettingsView>() :
                    UnityEngine.Object.FindFirstObjectByType<SongSelectionView>();
                var serialized = new SerializedObject(screen); serialized.FindProperty(field).objectReferenceValue = null;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.Throws<InvalidOperationException>(() => GameSceneValidator.Validate(scene));
            }
            finally { EditorSceneManager.OpenScene(SceneBuilder.BootstrapPath); }
        }
        [Test]
        public void MissingTapHeadAndGeneratedViewReferencesFail()
        {
            var scene = EditorSceneManager.OpenScene(SceneBuilder.GameplayPath);
            try
            {
                var serialized = new SerializedObject(UnityEngine.Object.FindFirstObjectByType<NotePresenter>());
                var element = serialized.FindProperty("views").GetArrayElementAtIndex(0);
                var view = new SerializedObject(element.objectReferenceValue);
                view.FindProperty("head").objectReferenceValue = null; view.ApplyModifiedPropertiesWithoutUndo();
                Assert.Throws<InvalidOperationException>(() => GameSceneValidator.Validate(scene));
                element.objectReferenceValue = null; serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.Throws<InvalidOperationException>(() => GameSceneValidator.Validate(scene));
            }
            finally { EditorSceneManager.OpenScene(SceneBuilder.BootstrapPath); }
        }
        [TestCase(4)] [TestCase(5)] [TestCase(6)]
        public void ModeCountAndCenteredLayoutHaveSingleSource(int lanes)
        {
            var mode = ScriptableObject.CreateInstance<GameModeDefinition>();
            try
            {
                mode.SetGeneratedDefaults(Enumerable.Range(0,lanes).Select(i => new LaneDefinition($"<Keyboard>/{i}",i.ToString(),Color.white)).ToArray());
                mode.Validate(); Assert.That(mode.ToRules().LaneCount, Is.EqualTo(lanes));
                Assert.That(mode.LaneX(0)+mode.LaneX(lanes-1), Is.EqualTo(0).Within(1e-6));
                if (lanes%2 != 0) Assert.That(mode.LaneX(lanes/2), Is.Zero);
            }
            finally { UnityEngine.Object.DestroyImmediate(mode); }
        }
        [Test]
        public void HoldViewUsesIntegratedDistanceAndPinsHoldingHead()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/HoldNote.prefab");
            var view = UnityEngine.Object.Instantiate(prefab).GetComponent<NoteView>();
            try
            {
                var song = Settings.catalog[1]; var scroll = Settings.scroll.CreateTimeline(ScrollMode.Bpm,song.Timing.ToTempoMap(),1);
                var hold = NoteData.Hold(0,5.2,6.8);
                view.Render(hold,NoteState.Pending,5,scroll,song.Mode,Settings.presentation);
                Assert.That(view.transform.Find("Head").position.z, Is.EqualTo(1.6f).Within(.0001));
                Assert.That(view.transform.Find("Hold Body").localScale.z, Is.EqualTo(16).Within(.0001));
                view.Render(hold,NoteState.Holding,6.2,scroll,song.Mode,Settings.presentation);
                Assert.That(view.transform.Find("Head").position.z, Is.Zero);
                Assert.That(view.transform.Find("Hold Body").localScale.z, Is.EqualTo(7.2f).Within(.0001));
            }
            finally { UnityEngine.Object.DestroyImmediate(view.gameObject); }
        }
        [Test]
        public void GeneratedScenesRemainStableAndSettingsAreNotOverwritten()
        {
            string before = EditorJsonUtility.ToJson(Settings.defaultDifficulty);
            string scrollBefore = EditorJsonUtility.ToJson(Settings.scroll);
            SceneBuilder.BuildAll(); string first = Signature(); SceneBuilder.BuildAll(); string second = Signature();
            Assert.That(second, Is.EqualTo(first));
            Assert.That(EditorJsonUtility.ToJson(Settings.defaultDifficulty), Is.EqualTo(before));
            Assert.That(EditorJsonUtility.ToJson(Settings.scroll), Is.EqualTo(scrollBefore));
            EditorSceneManager.OpenScene(SceneBuilder.BootstrapPath);
        }
        private static string Signature()
        {
            return string.Join("\n", new[] { SceneBuilder.BootstrapPath, SceneBuilder.SelectionPath, SceneBuilder.GameplayPath, SceneBuilder.SettingsPath }.Select(path =>
            {
                var scene = EditorSceneManager.OpenScene(path); GameSceneValidator.Validate(scene);
                return path + "\n" + string.Join("\n",scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true))
                    .Select(t=>$"{t.name}/{t.position}/{t.localScale}"));
            }));
        }
        [Test]
        public void DuplicateSongIdDisablesCatalogEntry()
        {
            var catalog = ScriptableObject.CreateInstance<SongCatalog>();
            try
            {
                catalog.SetGeneratedDefaults(new[] { Settings.catalog[0],Settings.catalog[0] });
                Assert.That(catalog.GetEntryError(0), Does.Contain("Duplicate"));
                Assert.Throws<InvalidOperationException>(()=>catalog.Validate());
            }
            finally { UnityEngine.Object.DestroyImmediate(catalog); }
        }
        [Test]
        public void DomainAndApplicationNeverReferenceUnityAssemblies()
        {
            foreach (var assembly in new[] { typeof(RhythmSession).Assembly,typeof(IScrollTimeline).Assembly })
                Assert.That(assembly.GetReferencedAssemblies().Any(a=>a.Name.StartsWith("Unity")), Is.False);
        }
    }
}
