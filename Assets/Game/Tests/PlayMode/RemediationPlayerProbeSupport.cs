using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RhythmDojo.Authoring;
using RhythmDojo.Services;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace RhythmDojo.Tests
{
    public sealed partial class RemediationPlayerProbe
    {
        [Serializable]
        private sealed class Report
        {
            public string phase, unity, platform, startedUtc, finishedUtc, step, error;
            public bool editor, success;
            public int assertions, requestedBuffer, appliedBuffer, sampleRate;
            public double duration, clipPeak, mixerPeak;
            public List<StepRecord> steps = new List<StepRecord>();
            public List<Measurement> clockMeasurements = new List<Measurement>();
            public List<Alignment> alignment = new List<Alignment>();
        }

        [Serializable]
        private sealed class StepRecord
        {
            public string name, detail, utc;
            public double elapsed;
        }

        [Serializable]
        private sealed class Measurement
        {
            public double elapsed, lower, upper;
            public int buffer, rate;
            public string state;
        }

        [Serializable]
        private sealed class Alignment
        {
            public double audio, logicalAudio, error, uncertainty;
            public int block, buffers;
        }

        [Serializable]
        private sealed class Preference
        {
            public string key, value;
            public bool existed;
        }

        [Serializable]
        private sealed class PreferenceBackup
        {
            public bool restored;
            public string persistentDataPath;
            public List<Preference> preferences = new List<Preference>();
            public List<string> ownedProjectIds = new List<string>();
        }

        private static readonly string[] preferenceKeys =
        {
            PlayKeyBindings.StorageKey,
            PlayKeyBindings.ModeStorageKey,
            SelectionPreferences.StorageKey,
            PresentationPreferences.StorageKey,
            HardModePreferences.StorageKey,
            PlayRulesService.StorageKey,
        };

        private IEnumerator RunChecked(IEnumerator routine)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(routine);
            while (stack.Count > 0)
            {
                object next = null;
                bool failed = false;
                try
                {
                    if (Time.realtimeSinceStartupAsDouble - started > 180)
                    {
                        throw new TimeoutException("Diagnostic exceeded 180 seconds.");
                    }
                    if (logFailure != null)
                    {
                        throw new InvalidOperationException("Unity reported an error: " + logFailure);
                    }
                    var current = stack.Peek();
                    if (!current.MoveNext())
                    {
                        (stack.Pop() as IDisposable)?.Dispose();
                        continue;
                    }
                    next = current.Current;
                    if (next is IEnumerator nested)
                    {
                        stack.Push(nested);
                        continue;
                    }
                }
                catch (Exception exception)
                {
                    report.error = exception.ToString();
                    failed = true;
                }
                if (failed)
                {
                    while (stack.Count > 0)
                    {
                        try
                        {
                            (stack.Pop() as IDisposable)?.Dispose();
                        }
                        catch (Exception exception)
                        {
                            report.error += "\nEnumerator cleanup: " + exception;
                        }
                    }
                    Flush();
                    yield break;
                }
                yield return next;
            }
            report.success = true;
        }

        private void ReceiveLog(string message, string trace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                logFailure ??= message + "\n" + trace;
            }
        }

        private static string Argument(string name, string fallback)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
            {
                if (args[i] == name)
                {
                    return args[i + 1];
                }
            }
            return fallback;
        }

        private void Require(bool condition, string description)
        {
            if (!condition)
            {
                throw new InvalidOperationException(description);
            }
            report.assertions++;
        }

        private void Step(string name, string detail)
        {
            report.step = name;
            report.steps.Add(new StepRecord
            {
                name = name,
                detail = detail,
                elapsed = Time.realtimeSinceStartupAsDouble - started,
                utc = DateTime.UtcNow.ToString("O"),
            });
            Flush();
        }

        private void Flush()
        {
            File.WriteAllText(Path.Combine(output, phase + ".json"), JsonUtility.ToJson(report, true));
        }

        private IEnumerator Wait(Func<bool> predicate, string description, double timeout = 15)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + timeout;
            while (!predicate())
            {
                Require(Time.realtimeSinceStartupAsDouble < deadline, "Timed out: " + description);
                yield return null;
            }
        }

        private static IEnumerator Delay(double seconds)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + seconds;
            while (Time.realtimeSinceStartupAsDouble < deadline)
            {
                yield return null;
            }
        }

        private IEnumerator Click(Button button)
        {
            Require(button && button.isActiveAndEnabled, "Click target is active.");
            Require(button.interactable, "Click target is interactable: " + button.name);
            yield return null;
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)button.transform;
            Vector2 point = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            var hits = new List<RaycastResult>();
            var system = EventSystem.current;
            Require(system, "An EventSystem handles the click.");
            system.RaycastAll(new PointerEventData(system) { position = point }, hits);
            Require(hits.Count > 0, "Raycast reaches " + button.name);
            Require(hits[0].gameObject.GetComponentInParent<Button>() == button,
                "Click target is unobscured: " + button.name + "; hit " + hits[0].gameObject.name);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point + Vector2.one });
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point }.WithButton(MouseButton.Left));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point + Vector2.one });
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = point });
            yield return null;
            yield return null;
        }

        private IEnumerator Press(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
        }

        private static double Peak(float[] samples)
        {
            double result = 0;
            foreach (float sample in samples)
            {
                result = Math.Max(result, Math.Abs(sample));
            }
            return result;
        }

        private void BackupPreferences()
        {
            string path = Path.Combine(output, "preferences-backup.json");
            if (File.Exists(path))
            {
                var existing = JsonUtility.FromJson<PreferenceBackup>(File.ReadAllText(path));
                Require(existing != null && existing.restored,
                    "An unfinished preference backup exists. Run persistence or restore first.");
            }
            backup = new PreferenceBackup { persistentDataPath = UnityEngine.Application.persistentDataPath };
            foreach (string key in preferenceKeys)
            {
                backup.preferences.Add(new Preference
                {
                    key = key,
                    value = PlayerPrefs.GetString(key, ""),
                    existed = PlayerPrefs.HasKey(key),
                });
            }
            SaveBackup();
            foreach (string key in preferenceKeys)
            {
                PlayerPrefs.DeleteKey(key);
            }
            PlayerPrefs.Save();
            Step("preferences-backed-up", "Known string preference keys and existence flags saved before isolation.");
        }

        private void LoadBackup()
        {
            var candidate = JsonUtility.FromJson<PreferenceBackup>(
                File.ReadAllText(Path.Combine(output, "preferences-backup.json")));
            Require(candidate != null, "Preference backup exists.");
            Require(candidate.persistentDataPath == UnityEngine.Application.persistentDataPath,
                "Backup belongs to this player data directory.");
            Require(candidate.preferences.Select(value => value.key).OrderBy(value => value)
                .SequenceEqual(preferenceKeys.OrderBy(value => value)), "Backup contains exactly the known keys.");
            backup = candidate;
        }

        private void SaveBackup()
        {
            string path = Path.Combine(output, "preferences-backup.json");
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(backup, true));
            if (File.Exists(path))
            {
                File.Replace(temporary, path, null);
            }
            else
            {
                File.Move(temporary, path);
            }
        }

        private void RestorePreferences()
        {
            if (backup == null || backup.restored)
            {
                return;
            }
            foreach (var preference in backup.preferences)
            {
                if (preference.existed)
                {
                    PlayerPrefs.SetString(preference.key, preference.value);
                }
                else
                {
                    PlayerPrefs.DeleteKey(preference.key);
                }
            }
            PlayerPrefs.Save();
            foreach (var preference in backup.preferences)
            {
                Require(PlayerPrefs.HasKey(preference.key) == preference.existed, "Preference existence restored.");
                if (preference.existed)
                {
                    Require(PlayerPrefs.GetString(preference.key) == preference.value, "Preference value restored.");
                }
            }
            backup.restored = true;
            SaveBackup();
        }

        private string OwnedProjectFolder(string id)
        {
            Require(Guid.TryParseExact(id, "D", out _), "Owned project identifier is a GUID.");
            string root = Path.GetFullPath(Path.Combine(UnityEngine.Application.persistentDataPath, "Songs"));
            string folder = Path.GetFullPath(Path.Combine(root, id));
            Require(string.Equals(Path.GetDirectoryName(folder), root, StringComparison.OrdinalIgnoreCase),
                "Owned project folder stays directly within Songs.");
            foreach (string item in new[] { root, folder })
            {
                if (Directory.Exists(item))
                {
                    Require((File.GetAttributes(item) & FileAttributes.ReparsePoint) == 0,
                        "Owned project cleanup refuses linked directories.");
                }
            }
            return folder;
        }

        private void CleanupProjects()
        {
            if (backup == null)
            {
                return;
            }
            foreach (string id in backup.ownedProjectIds)
            {
                string folder = OwnedProjectFolder(id);
                if (!Directory.Exists(folder))
                {
                    continue;
                }
                string file = Path.Combine(folder, FileSongProjectStore.FileName);
                Require(Directory.GetDirectories(folder).Length == 0, "Owned draft has no unexpected child directories.");
                Require(Directory.GetFiles(folder).All(value => value == file), "Owned draft has only its expected JSON file.");
                if (File.Exists(file))
                {
                    Require((File.GetAttributes(file) & FileAttributes.ReparsePoint) == 0,
                        "Owned draft cleanup refuses linked files.");
                    File.Delete(file);
                }
                Directory.Delete(folder, false);
            }
            backup.ownedProjectIds.Clear();
            SaveBackup();
        }

        private void Cleanup()
        {
            var failures = new List<Exception>();
            TryCleanup(CleanupProjects, failures);
            TryCleanup(() =>
            {
                if (flow)
                {
                    Object.Destroy(flow.gameObject);
                }
            }, failures);
            TryCleanup(() => wave?.Dispose(), failures);
            TryCleanup(() =>
            {
                if (mode)
                {
                    Object.Destroy(mode);
                }
            }, failures);
            if (initializedInput)
            {
                TryCleanup(() => InputSystem.RemoveDevice(keyboard), failures);
                TryCleanup(() => InputSystem.RemoveDevice(mouse), failures);
                InputSystem.settings.backgroundBehavior = previousBackground;
            }
            TryCleanup(() => Require(AudioSettings.Reset(originalAudio),
                "Original process audio configuration restored."), failures);
            if (failures.Count > 0)
            {
                throw new AggregateException("Diagnostic cleanup failed.", failures);
            }
        }

        private static void TryCleanup(Action action, List<Exception> failures)
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        private IEnumerator Capture(string name)
        {
            yield return null;
            // Gameplay's MainCamera feeds a RawImage, while a separate camera presents the UI.
            // Select by output ownership rather than camera names or tags.
            var camera = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)
                .Where(value => value.isActiveAndEnabled && !value.targetTexture)
                .Where(value => value.targetDisplay == 0)
                .OrderByDescending(value => value.depth).FirstOrDefault();
            Require(camera, "An active display camera exists for offscreen capture: " + name);
            Require(Screen.width > 0 && Screen.height > 0, "Player display dimensions are available.");
            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)
                .Where(canvas => canvas.isRootCanvas && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                .Select(canvas => (canvas, canvas.worldCamera, canvas.planeDistance)).ToArray();
            // Match the player dimensions so GameplayViewport keeps its projection and texture size.
            var target = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            var previousTarget = camera.targetTexture;
            int previousMask = camera.cullingMask;
            var suspendedRenderers = new List<Renderer>();
            try
            {
                target.Create();
                camera.targetTexture = target;
                foreach (var item in canvases)
                {
                    camera.cullingMask |= 1 << item.canvas.gameObject.layer;
                    item.canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    item.canvas.worldCamera = camera;
                    item.canvas.planeDistance = 1;
                }
                Canvas.ForceUpdateCanvases();

                // Refresh existing offscreen sources before the UI samples their RawImages.
                foreach (var source in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)
                    .Where(value => value.isActiveAndEnabled && value.targetTexture)
                    .Where(value => value != camera).OrderBy(value => value.depth))
                {
                    RenderOffscreen(source, source.targetTexture);
                }
                // Canvas layers may also contain world meshes. Preserve the display camera's
                // original world visibility after filling the gameplay RawImage sources.
                foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                {
                    if (!renderer.enabled)
                    {
                        continue;
                    }
                    if ((previousMask & (1 << renderer.gameObject.layer)) != 0)
                    {
                        continue;
                    }
                    suspendedRenderers.Add(renderer);
                    renderer.enabled = false;
                }
                RenderOffscreen(camera, target);
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                texture.Apply();
                var pixels = texture.GetPixels32();
                int visible = 0;
                foreach (var pixel in pixels)
                {
                    if (pixel.r > 8 || pixel.g > 8 || pixel.b > 8)
                    {
                        visible++;
                    }
                }
                Require(visible > pixels.Length / 1000,
                    "Screenshot must contain visible content rather than a black frame: " + name);
                File.WriteAllBytes(Path.Combine(output, phase + "-" + name + ".png"), texture.EncodeToPNG());
                Step("capture-" + name, "Offscreen StandardRequest display camera=" + camera.name
                    + "; size=" + texture.width + "x" + texture.height + "; visible pixels=" + visible);
            }
            finally
            {
                foreach (var renderer in suspendedRenderers)
                {
                    if (renderer)
                    {
                        renderer.enabled = true;
                    }
                }
                foreach (var item in canvases)
                {
                    item.canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    item.canvas.worldCamera = item.worldCamera;
                    item.canvas.planeDistance = item.planeDistance;
                }
                camera.targetTexture = previousTarget;
                camera.cullingMask = previousMask;
                RenderTexture.active = previous;
                target.Release();
                Object.Destroy(target);
                Object.Destroy(texture);
                Canvas.ForceUpdateCanvases();
            }
        }

        private void RenderOffscreen(Camera camera, RenderTexture target)
        {
            var request = new RenderPipeline.StandardRequest { destination = target };
            Require(RenderPipeline.SupportsRenderRequest(camera, request),
                "Active render pipeline supports StandardRequest for " + camera.name);
            RenderPipeline.SubmitRenderRequest(camera, request);
        }
    }
}


