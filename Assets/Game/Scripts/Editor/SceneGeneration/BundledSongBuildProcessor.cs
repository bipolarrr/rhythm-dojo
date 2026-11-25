using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using RhythmDojo.Application;
using RhythmDojo.Authoring;
using RhythmDojo.Gameplay;
using UnityEditor;
using UnityEditor.Build;

namespace RhythmDojo.EditorTools
{
    public sealed class BundledSongBuildProcessor : BuildPlayerProcessor
    {
        public override void PrepareForBuild(BuildPlayerContext buildPlayerContext)
        {
            var target = buildPlayerContext.BuildPlayerOptions.target;
            if (target != BuildTarget.StandaloneWindows && target != BuildTarget.StandaloneWindows64)
            {
                return;
            }

            var settings = AssetDatabase.LoadAssetAtPath<GameSettings>(TestContentBuilder.SettingsPath);
            var root = Path.Combine(UnityEngine.Application.dataPath, "../Content");
            AddSongs(root, settings.catalog[0].Mode, buildPlayerContext.AddAdditionalPathToStreamingAssets);
        }

        internal static void AddSongs(string root, GameModeDefinition mode, Action<string, string> addFile)
        {
            var ids = new HashSet<Guid>();
            foreach (var folder in Directory.GetDirectories(root).OrderBy(path => path, StringComparer.Ordinal))
            {
                string path = Path.Combine(folder, FileSongProjectStore.FileName);
                string title = Path.GetFileName(folder);
                try
                {
                    var project = new SongProjectJson().Deserialize(File.ReadAllText(path));
                    title = project.Title;
                    if (!ids.Add(project.Id))
                    {
                        throw new InvalidDataException("Duplicate song ID: " + project.Id);
                    }

                    if (project.Audio == null || !project.Charts.Any(chart => chart.Slot.HasValue))
                    {
                        throw new InvalidDataException("A bundled song requires audio and a playable chart slot.");
                    }

                    string audioPath = Path.Combine(folder, project.Audio);
                    foreach (var chart in project.Charts.Where(chart => chart.Slot.HasValue))
                    {
                        ChartPlaybackAdapter.ToDocument(project, chart.Id, mode.name, mode.ToRules(),
                            audio => File.Exists(Path.Combine(folder, audio)));
                    }

                    using var hash = SHA256.Create();
                    using var stream = File.OpenRead(audioPath);
                    string digest = BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "");
                    if (!digest.Equals(Path.GetFileNameWithoutExtension(audioPath), StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException("Audio SHA-256 mismatch: " + audioPath);
                    }

                    string destination = "Songs/" + project.Id.ToString("D") + "/";
                    addFile(Path.GetFullPath(path), destination + FileSongProjectStore.FileName);
                    addFile(Path.GetFullPath(audioPath), destination + project.Audio);
                }
                catch (Exception exception)
                {
                    throw new BuildFailedException($"Cannot bundle '{title}' ({path}): {exception.Message}");
                }
            }

            if (ids.Count == 0)
            {
                throw new BuildFailedException("No bundled song projects in " + root);
            }
        }
    }
}
