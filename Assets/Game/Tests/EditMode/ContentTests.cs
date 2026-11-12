using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using RhythmDojo.Application;
using RhythmDojo.Core;
using RhythmDojo.EditorTools;
using RhythmDojo.Gameplay;
using RhythmDojo.Presentation;
using RhythmDojo.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace RhythmDojo.Tests
{
    [Category("Gameplay")]
    public sealed class ContentTests
    {
        private GameSettings Settings =>
            AssetDatabase.LoadAssetAtPath<GameSettings>(TestContentBuilder.SettingsPath);

        [Test]
        [Category("Fast")]
        public void SettingsAndTwoSongsAreValid()
        {
            Settings.Validate();
            Assert.That(Settings.catalog.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(Settings.catalog[0].Chart.Count, Is.EqualTo(18));
            Assert.That(Settings.catalog[1].Chart.Count, Is.EqualTo(8));
            Assert.That(Settings.catalog[1].Timing.ToTempoMap().BpmAt(6), Is.EqualTo(180));
        }

        // Preserve exact asset bytes and meta files so migration tests retain original GUIDs.
        private static void WithSettingsRestored(Action test)
        {
            using var snapshot = new GeneratedSceneSnapshot();
            test();
        }

        private static void DeleteMigrationTargets()
        {
            foreach (
                var name in new[]
                {
                    "Standard",
                    "ScrollSettings",
                    "AudioPlaybackSettings",
                    "TestSong",
                }
            )
            {
                Assert.That(
                    AssetDatabase.DeleteAsset($"Assets/Game/Settings/{name}.asset"),
                    Is.True
                );
            }

            AssetDatabase.DeleteAsset("Assets/Game/Settings/SongCatalog.asset");
        }

        private static void SetNumber(UnityEngine.Object target, string field, double value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).doubleValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(target);
        }

        [Test]
        [Category("AssetGeneration")]
        public void FirstCreationMigratesLegacyValuesAndOffset()
        {
            WithSettingsRestored(() =>
            {
                var legacy = AssetDatabase.LoadAssetAtPath<RhythmGameConfig>(
                    SceneBuilder.ConfigPath
                );
                SetNumber(legacy, "perfectWindow", .06);
                SetNumber(legacy, "goodWindow", .12);
                SetNumber(legacy, "scrollSpeed", 9);
                SetNumber(legacy, "scheduleLeadTime", .35);
                SetNumber(legacy, "audioOffset", .08);
                DeleteMigrationTargets();
                AssetDatabase.DeleteAsset(TestContentBuilder.SettingsPath);

                var generated = TestContentBuilder.Prepare().Settings;
                Assert.That(
                    generated.defaultDifficulty.ToSettings().PerfectWindow,
                    Is.EqualTo(.06)
                );
                Assert.That(generated.defaultDifficulty.ToSettings().GoodWindow, Is.EqualTo(.12));
                Assert.That(generated.audio.ScheduleLeadTime, Is.EqualTo(.35));
                Assert.That(
                    generated
                        .scroll.CreateTimeline(
                            ScrollMode.Constant,
                            new SongTiming().ToTempoMap(),
                            1
                        )
                        .DistanceBetween(0, 1),
                    Is.EqualTo(9)
                );
                Assert.That(generated.catalog[0].Timing.chartAudioOffsetSeconds, Is.EqualTo(.08));
                generated.defaultDifficulty = generated.difficulties.Single(d => d.DisplayName == "Easy");
                generated.audio.MigrateLeadTime(.23);
                ContentAssets.Dirty(generated.audio);
                ContentAssets.Dirty(generated);
                string audioBefore = EditorJsonUtility.ToJson(generated.audio);
                string songBefore = EditorJsonUtility.ToJson(generated.catalog[0]);
                string guid = AssetDatabase.AssetPathToGUID("Assets/Game/Settings/AudioPlaybackSettings.asset");
                SetNumber(legacy, "goodWindow", -1);
                ContentAssets.Dirty(legacy);
                generated = TestContentBuilder.Prepare().Settings;
                Assert.That(generated.defaultDifficulty.DisplayName, Is.EqualTo("Easy"));
                Assert.That(EditorJsonUtility.ToJson(generated.audio), Is.EqualTo(audioBefore));
                Assert.That(EditorJsonUtility.ToJson(generated.catalog[0]), Is.EqualTo(songBefore));
                Assert.That(AssetDatabase.AssetPathToGUID("Assets/Game/Settings/AudioPlaybackSettings.asset"), Is.EqualTo(guid));
            });
        }

        [Test]
        [Category("Fast")]
        public void FreshCreationWithoutLegacyUsesNewDefaults()
        {
            var standard = ScriptableObject.CreateInstance<DifficultyProfile>();
            var audio = ScriptableObject.CreateInstance<AudioPlaybackSettings>();
            var scroll = ScriptableObject.CreateInstance<ScrollSettings>();
            try
            {
                var legacy = DefaultSettingsBuilder.MigrationSource(true, false,
                    () => throw new Exception("Absent legacy must not load."));
                DefaultSettingsBuilder.InitializeStandard(standard, legacy);
                DefaultSettingsBuilder.InitializeAudio(audio, legacy);
                DefaultSettingsBuilder.InitializeScroll(scroll, legacy);
                var timing = new SongTiming();
                DefaultSettingsBuilder.InitializeTiming(timing, legacy);
                Assert.That(audio.ScheduleLeadTime, Is.EqualTo(.15));
                Assert.That(standard.ToSettings().PerfectWindow, Is.EqualTo(.05));
                Assert.That(standard.ToSettings().GoodWindow, Is.EqualTo(.11));
                Assert.That(timing.chartAudioOffsetSeconds, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(standard);
                UnityEngine.Object.DestroyImmediate(audio);
                UnityEngine.Object.DestroyImmediate(scroll);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        [Category("Fast")]
        public void CompletedMigrationPreservesEasyAndAudioWhenLegacyIsInvalidOrAbsent(bool absent)
        {
            var legacy = ScriptableObject.CreateInstance<RhythmGameConfig>();
            try
            {
                SetNumber(legacy, "goodWindow", -1);
                bool loaded = false;
                Assert.That(DefaultSettingsBuilder.MigrationSource(false, !absent, () =>
                {
                    loaded = true;
                    return legacy;
                }), Is.Null);
                Assert.That(loaded, Is.False, "Completed migration must not inspect invalid legacy data.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(legacy);
            }
        }

        [Test]
        [Category("Fast")]
        public void InvalidLegacyFailsOnlyWhenMigrationIsNeeded()
        {
            var legacy = ScriptableObject.CreateInstance<RhythmGameConfig>();
            try
            {
                SetNumber(legacy, "goodWindow", -1);
                Assert.Throws<InvalidOperationException>(() =>
                    DefaultSettingsBuilder.MigrationSource(true, true, () => legacy));
                Assert.Throws<InvalidOperationException>(() =>
                    DefaultSettingsBuilder.MigrationSource(true, true, () => null));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(legacy);
            }
        }

        [Test]
        [Category("Fast")]
        public void GeneratedViewValidationUsesChartKindsAndRuntimeCanRebuildViews()
        {
            var presenter = CreatePresenter();
            var chart = Settings.catalog[0].Chart.ToChartData();
            try
            {
                var serialized = new SerializedObject(presenter);
                var views = serialized.FindProperty("views");
                var tap = (NoteView)views.GetArrayElementAtIndex(0).objectReferenceValue;
                tap.name = "Hold named tap";
                Assert.DoesNotThrow(() => presenter.Validate(chart));
                var hold = (NoteView)views.GetArrayElementAtIndex(4).objectReferenceValue;
                hold.name = "Unnamed note";
                var holdFields = new SerializedObject(hold);
                holdFields.FindProperty("body").objectReferenceValue = null;
                holdFields.ApplyModifiedPropertiesWithoutUndo();
                Assert.Throws<InvalidOperationException>(() => presenter.Validate(chart));
                Assert.DoesNotThrow(() => presenter.Validate());
                views.ClearArray();
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.Throws<InvalidOperationException>(() => presenter.Validate(chart));
                Assert.DoesNotThrow(() => presenter.Validate());
                presenter.Initialize(chart, Settings.catalog[0].Mode.ToRules(), new ConstantScrollTimeline(8, 1));
                Assert.DoesNotThrow(() => presenter.Validate(chart));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(presenter.gameObject);
            }
        }

        private NotePresenter CreatePresenter()
        {
            var presenter = new GameObject("Note validation").AddComponent<NotePresenter>();
            var serialized = new SerializedObject(presenter);
            serialized.FindProperty("tapTemplate").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/TapNote.prefab").GetComponent<NoteView>();
            serialized.FindProperty("holdTemplate").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/HoldNote.prefab").GetComponent<NoteView>();
            serialized.FindProperty("noteMaterial").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Materials/Note Base.mat");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            presenter.Configure(Settings.catalog[0].Mode, Settings.presentation);
            presenter.Initialize(Settings.catalog[0].Chart.ToChartData(), Settings.catalog[0].Mode.ToRules(),
                new ConstantScrollTimeline(8, 1));
            return presenter;
        }

        [TestCase("Bootstrap", "settings")]
        [TestCase("SongSelection", "settingsButton")]
        [TestCase("SongSelection", "practice")]
        [TestCase("Settings", "bufferList")]
        [TestCase("Settings", "actualState")]
        [TestCase("Settings", "status")]
        [TestCase("Settings", "apply")]
        [TestCase("Settings", "back")]
        [TestCase("Settings", "settings")]
        [Category("Fast")]
        public void MissingRequiredScreenReferencesFail(string sceneName, string field)
        {
            MonoBehaviour screen;
            Action validate;
            if (sceneName == "Bootstrap")
            {
                var bootstrap = new GameObject("Bootstrap validation").AddComponent<Bootstrap>();
                SceneDependencyAssembler.Wire(bootstrap, "settings", Settings);
                screen = bootstrap;
                validate = bootstrap.Validate;
            }
            else if (sceneName == "SongSelection")
            {
                var view = SongSelectionLayoutBuilder.Build();
                screen = view;
                validate = view.Validate;
            }
            else
            {
                var clone = UnityEngine.Object.Instantiate(
                    AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Resources/UI/SettingsPopup.prefab"));
                var view = clone.GetComponent<SettingsView>();
                screen = view;
                validate = view.Validate;
                if (field == "settings")
                {
                    var controller = clone.GetComponent<SettingsScreen>();
                    var root = clone.AddComponent<ScreenCompositionRoot>();
                    SceneDependencyAssembler.Wire(root, "settings", Settings);
                    SceneDependencyAssembler.Wire(root, "audioSettings", controller);
                    screen = root;
                    validate = root.Validate;
                }
            }

            try
            {
                Assert.DoesNotThrow(() => validate(), "The isolated view must be valid before removing a reference.");
                var serialized = new SerializedObject(screen);
                serialized.FindProperty(field).objectReferenceValue = null;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.Throws<InvalidOperationException>(() => validate());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(screen.gameObject);
            }
        }

        [Test]
        [Category("Fast")]
        public void MissingTapHeadAndGeneratedViewReferencesFail()
        {
            var presenter = CreatePresenter();
            var chart = Settings.catalog[0].Chart.ToChartData();
            try
            {
                var serialized = new SerializedObject(presenter);
                var element = serialized.FindProperty("views").GetArrayElementAtIndex(0);
                var view = new SerializedObject(element.objectReferenceValue);
                view.FindProperty("head").objectReferenceValue = null;
                view.ApplyModifiedPropertiesWithoutUndo();
                Assert.Throws<InvalidOperationException>(() => presenter.Validate(chart));
                element.objectReferenceValue = null;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.Throws<InvalidOperationException>(() => presenter.Validate(chart));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(presenter.gameObject);
            }
        }

        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        [Category("Fast")]
        public void ModeCountAndCenteredLayoutHaveSingleSource(int lanes)
        {
            var mode = ScriptableObject.CreateInstance<GameModeDefinition>();

            try
            {
                mode.SetGeneratedDefaults(
                    Enumerable
                        .Range(0, lanes)
                        .Select(i => new LaneDefinition(
                            $"<Keyboard>/{i}",
                            i.ToString(),
                            Color.white
                        ))
                        .ToArray()
                );
                mode.Validate();
                Assert.That(mode.ToRules().LaneCount, Is.EqualTo(lanes));
                Assert.That(mode.LaneX(0) + mode.LaneX(lanes - 1), Is.EqualTo(0).Within(1e-6));

                if (lanes % 2 != 0)
                {
                    Assert.That(mode.LaneX(lanes / 2), Is.Zero);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mode);
            }
        }

        [Test]
        [Category("Fast")]
        public void HoldViewUsesIntegratedDistanceAndPinsHoldingHead()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Game/Prefabs/HoldNote.prefab"
            );
            var view = UnityEngine.Object.Instantiate(prefab).GetComponent<NoteView>();

            try
            {
                var song = Settings.catalog[1];
                var scroll = Settings.scroll.CreateTimeline(
                    ScrollMode.Bpm,
                    song.Timing.ToTempoMap(),
                    1
                );
                var hold = NoteData.Hold(0, 5.2, 6.8);
                view.Render(hold, NoteState.Pending, 5, scroll, song.Mode, Settings.presentation);

                // 150 BPM gives 10 units/s before 6s; 180 BPM gives 12 units/s afterwards.
                Assert.That(view.transform.Find("Head").position.z, Is.EqualTo(2f).Within(.0001));
                Assert.That(
                    view.transform.Find("Hold Body").localScale.z,
                    Is.EqualTo(17.6f).Within(.0001)
                );
                view.Render(hold, NoteState.Holding, 6.2, scroll, song.Mode, Settings.presentation);
                Assert.That(view.transform.Find("Head").position.z, Is.Zero);
                Assert.That(
                    view.transform.Find("Hold Body").localScale.z,
                    Is.EqualTo(7.2f).Within(.0001)
                );
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(view.gameObject);
            }
        }

        [Test]
        [Category("Fast")]
        public void DuplicateSongIdDisablesCatalogEntry()
        {
            var catalog = ScriptableObject.CreateInstance<SongCatalog>();

            try
            {
                catalog.SetGeneratedDefaults(new[] { Settings.catalog[0], Settings.catalog[0] });
                Assert.That(catalog.GetEntryError(0), Does.Contain("Duplicate"));
                Assert.Throws<InvalidOperationException>(() => catalog.Validate());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        [Category("Fast")]
        public void DomainAndApplicationNeverReferenceUnityAssemblies()
        {
            foreach (
                var assembly in new[]
                {
                    typeof(RhythmSession).Assembly,
                    typeof(IScrollTimeline).Assembly,
                }
            )
            {
                Assert.That(
                    assembly.GetReferencedAssemblies().Any(a => a.Name.StartsWith("Unity")),
                    Is.False
                );
            }
        }
    }
}
