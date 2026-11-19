using System;
using System.Collections.Generic;
using System.IO;
using RhythmDojo.Application;
using RhythmDojo.Content;
using RhythmDojo.Dialogue;
using RhythmDojo.Gameplay;
using UnityEditor;
using UnityEngine;
using static RhythmDojo.EditorTools.ContentAssets;

namespace RhythmDojo.EditorTools
{
    // The original guided lesson. Changes to these charts/criteria require a version bump.
    public static class TrillContentBuilder
    {
        private const string Folder = "Assets/Game/Settings/Training";

        public static TrainingLesson Prepare()
        {
            Directory.CreateDirectory(Folder + "/Dialogue");
            AssetDatabase.Refresh();
            var audio = PreparePulse();
            var notes = new List<NoteData>();
            Pattern(notes, 0, 2, 4, 2.4, .6);
            Pattern(notes, 1, 3, 4, 7.2, .6);
            var imitation = Song("TrillImitation", "트릴 · 따라 하기", notes, 10.2, audio);

            notes.Clear();
            Pattern(notes, 0, 2, 4, 2.4, .3);
            Pattern(notes, 1, 3, 4, 7.2, .3);
            Pattern(notes, 0, 2, 8, 12, .3);
            Pattern(notes, 1, 3, 8, 16.8, .3);
            var application = Song("TrillApplication", "트릴 · 응용", notes, 20.1, audio);

            notes.Clear();
            Pattern(notes, 0, 2, 4, 2.4, .6);
            Pattern(notes, 1, 3, 4, 7.2, .6);
            Pattern(notes, 0, 2, 8, 12, .3);
            Pattern(notes, 1, 3, 8, 16.8, .3);
            var assessment = Song("TrillAssessment", "트릴 · 검정", notes, 20.1, audio);

            var lesson = Asset<TrainingLesson>(TrainingContentBuilder.LessonPath, out _);
            var serialized = new SerializedObject(lesson);
            serialized.FindProperty("lessonId").stringValue = "trill";
            serialized.FindProperty("title").stringValue = "트릴";
            serialized.FindProperty("patternDescription").stringValue = "1-3-1-3 / 2-4-2-4 · BPM 100";
            serialized.FindProperty("definitionVersion").intValue = 1;
            serialized.FindProperty("guided").boolValue = true;
            serialized.FindProperty("stage").enumValueIndex = (int)TrainingStage.Assessment;
            serialized.FindProperty("song").objectReferenceValue = assessment;
            serialized.FindProperty("imitationSong").objectReferenceValue = imitation;
            serialized.FindProperty("applicationSong").objectReferenceValue = application;
            serialized.FindProperty("maximumMisses").intValue = 0;
            serialized.FindProperty("minimumAccuracy").doubleValue = 90;

            Dialogue(serialized, "introductionDialogue", "TrillIntroduction",
                Line("welcome", "왔느냐? 오늘 전수할 것은 트릴이다. 두 자리를 번갈아, 고른 간격으로 두드리는 기술이지.", DialogueExpression.Calm),
                Line("teach", "왼쪽부터 1·2·3·4번 레인이다. 먼저 1-3-1-3, 쉬었다가 2-4-2-4. 한 박에 한 번, 네 타씩 친다.", DialogueExpression.Teaching),
                Line("watch", "BPM 100의 느린 박자다. 내가 먼저 보여주마. 손을 쉬고, 노트와 타격 순간을 잘 보거라.", DialogueExpression.Confident));
            Dialogue(serialized, "imitationDialogue", "TrillImitation",
                Line("imitate", "이제 네 차례다. 같은 채보를 네 손으로 쳐 보거라. 네 박을 들은 뒤 시작한다. 쉼표에서는 손도 쉬어라.", DialogueExpression.Teaching));
            Dialogue(serialized, "applicationDialogue", "TrillApplication",
                Line("shorter", "좋다. 이제 한 박에 두 번, 간격을 절반으로 줄인다. 1-3, 2-4 조합은 그대로다.", DialogueExpression.Confident),
                Line("longer", "처음 두 묶음은 네 타, 다음 두 묶음은 여덟 타다. 길어져도 간격을 고르게. 충분히 쉰 뒤 다시 시작하자.", DialogueExpression.Teaching));
            Dialogue(serialized, "assessmentDialogue", "TrillAssessment",
                Line("test", "마지막 검정이다. 느린 네 타를 두 묶음, 빠른 여덟 타를 두 묶음. 네 타격을 지켜보마.", DialogueExpression.Serious),
                Line("criteria", "끝까지 치고, Miss 없이 정확도 90% 이상이면 된다. 노트에 맞지 않는 추가 입력도 없어야 한다. 준비되면 시작하자.", DialogueExpression.Confident));
            Dialogue(serialized, "demonstrationFinishedDialogue", "TrillDemonstrated",
                Line("observed", "보았느냐? 두 자리를 번갈아 네 번. 소리가 멈추면 쉬었다가, 다음 묶음도 같은 간격으로 시작한다.", DialogueExpression.Teaching));
            Dialogue(serialized, "practiceSucceededDialogue", "TrillPracticePassed",
                Line("praise", "잘했다. 번갈아 치는 손끝에 박자가 익고 있구나. 다음 단계로 가 보자.", DialogueExpression.Praising));
            Dialogue(serialized, "successDialogue", "TrillPassed",
                Line("praise", "훌륭하다. 짧은 트릴도 긴 트릴도 고르게 이어냈구나. 트릴 수련을 이수했다.", DialogueExpression.Praising),
                Line("complete", "이수 기록을 남겨 두었다. 수련 선택으로 돌아가도 좋고, 검정에 다시 도전해도 좋다.", DialogueExpression.Calm));
            Dialogue(serialized, "failureDialogue", "TrillFailed",
                Line("retry", "아직 이 단계의 조건에 닿지 못했구나. 방금 기록을 살펴보고, 같은 단계에 다시 도전해 보자.", DialogueExpression.Serious));
            Dialogue(serialized, "missingDialogue", "TrillMissing",
                Line("missing", "맞히지 못한 노트가 있다. 쉼표 뒤 첫 박을 듣고, 묶음마다 네 타인지 여덟 타인지 세어 보거라.", DialogueExpression.Teaching));
            Dialogue(serialized, "timingDialogue", "TrillTiming",
                Line("timing", "맞힌 노트 중 Perfect 범위를 벗어난 타격이 있다. 두 손 사이의 간격을 고르게 유지해 보거라.", DialogueExpression.Teaching));
            Dialogue(serialized, "extraInputDialogue", "TrillExtra",
                Line("extra", "노트에 맞지 않은 입력이 기록되었다. 내려오는 노트의 자리와 순간을 정확히 보고 치거라. 쉼표에서는 기다리고.", DialogueExpression.Teaching));
            Dialogue(serialized, "abortedDialogue", "TrillAborted",
                Line("rest", "잠시 쉬어도 좋다. 중단한 단계부터 다시 시작할 수 있다. 수련 선택으로 돌아가면 처음부터 살펴보자.", DialogueExpression.Calm));
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Dirty(lesson);
            lesson.Validate();
            return lesson;
        }

        private static void Pattern(List<NoteData> notes, int first, int second, int count, double start, double interval)
        {
            for (int i = 0; i < count; i++)
            {
                int lane = first;
                if (i % 2 != 0)
                {
                    lane = second;
                }
                notes.Add(NoteData.Tap(lane, start + i * interval));
            }
        }

        private static SongDefinition Song(string name, string title, List<NoteData> notes, double end, AudioClip audio)
        {
            var chart = Asset<RhythmChart>(Folder + "/" + name + "Chart.asset", out _);
            chart.SetGeneratedContent(notes.ToArray(), end);
            Dirty(chart);
            var song = Asset<SongDefinition>(Folder + "/" + name + "Song.asset", out _);
            song.SetGeneratedDefaults(name, title, audio, chart,
                SceneResources.Require<GameModeDefinition>("Assets/Game/Settings/FourLaneMode.asset"),
                new SongTiming { tempoPoints = new[] { new SerializedTempoPoint(0, 100) } });
            Dirty(song);
            return song;
        }

        private static DialogueSequenceAsset.Line Line(string id, string text, DialogueExpression expression)
        {
            return new DialogueSequenceAsset.Line
            {
                id = id,
                text = text,
                expression = expression,
                motion = DialogueMotion.Nod,
            };
        }

        private static void Dialogue(SerializedObject lesson, string field, string name,
            params DialogueSequenceAsset.Line[] lines)
        {
            var sequence = Asset<DialogueSequenceAsset>(Folder + "/Dialogue/" + name + ".asset", out _);
            sequence.sequenceId = "trill-" + name;
            sequence.version = 1;
            sequence.lines = lines;
            sequence.CreateLines();
            Dirty(sequence);
            lesson.FindProperty(field).objectReferenceValue = sequence;
        }

        private static AudioClip PreparePulse()
        {
            const string path = Folder + "/TrillPulse.wav";
            // Deterministic 100 BPM count-in and reference pulse, baked once in the editor.
            if (!File.Exists(path))
            {
                const int rate = 44100;
                const int samples = rate * 22;
                using (var writer = new BinaryWriter(File.Create(path)))
                {
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                    writer.Write(36 + samples * 2);
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                    writer.Write(16);
                    writer.Write((short)1);
                    writer.Write((short)1);
                    writer.Write(rate);
                    writer.Write(rate * 2);
                    writer.Write((short)2);
                    writer.Write((short)16);
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                    writer.Write(samples * 2);
                    for (int i = 0; i < samples; i++)
                    {
                        double time = (i % 26460) / (double)rate;
                        double value = 0;
                        if (time < .045)
                        {
                            value = .24 * Math.Sin(time * 2 * Math.PI * 880) * Math.Exp(-time * 110);
                        }
                        writer.Write((short)(value * short.MaxValue));
                    }
                }
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                var settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.PCM;
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
            }
            return SceneResources.Require<AudioClip>(path);
        }
    }
}
