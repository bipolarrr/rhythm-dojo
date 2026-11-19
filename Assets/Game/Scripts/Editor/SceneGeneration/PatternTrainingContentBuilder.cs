using System;
using System.Collections.Generic;
using System.Linq;
using RhythmDojo.Application;
using RhythmDojo.Content;
using RhythmDojo.Dialogue;
using RhythmDojo.Gameplay;
using UnityEditor;
using UnityEngine;
using static RhythmDojo.EditorTools.ContentAssets;

namespace RhythmDojo.EditorTools
{
    // Authored lesson data for the existing Tutorial/Gameplay flow. Bump the lesson
    // version when changing a chart, its criteria, or its rules after release.
    public static class PatternTrainingContentBuilder
    {
        private const string Folder = "Assets/Game/Settings/Training/";

        private sealed class Teaching
        {
            public string Introduction;
            public string Explanation;
            public string Practice;
            public string Application;
            public string Assessment;
            public string Observed;
            public string Praise;
            public string Missing;
            public string Timing;
            public string Extra;
        }

        public static TrainingLesson[] Prepare(AudioClip audio)
        {
            return new[]
            {
                Stairs(audio), Jack(audio), Burst(audio), HoldTap(audio), Shield(audio), ChordTrill(audio),
            };
        }

        private static TrainingLesson Stairs(AudioClip audio)
        {
            var imitation = new List<NoteData>();
            Taps(imitation, 2.4, .6, 0, 1, 2, 3);
            Taps(imitation, 7.2, .6, 3, 2, 1, 0);
            var application = new List<NoteData>();
            Taps(application, 2.4, .3, 0, 1, 2, 3, 2, 1, 0);
            Chords(application, 7.2, .6, new[] { 0, 3 }, new[] { 1, 2 }, new[] { 0, 2 }, new[] { 1, 3 });
            var assessment = new List<NoteData>();
            Taps(assessment, 2.4, .3, 0, 1, 2, 3);
            Taps(assessment, 6, .3, 3, 2, 1, 0);
            Chords(assessment, 9.6, .6, new[] { 0, 3 }, new[] { 1, 2 }, new[] { 1, 3 }, new[] { 0, 2 });
            Taps(assessment, 14.4, .3, 3, 2, 1, 0, 1, 2, 3);
            return Lesson("StairsDenim", "stairs-denim", "계단·데님", "순서와 방향 · 교차 동시치기 · BPM 100",
                audio, new[] { imitation, application, assessment }, new Teaching
                {
                    Introduction = "계단은 발끝보다 눈이 먼저 오르는 법. 오늘은 손가락으로 길을 읽어 보자.",
                    Explanation = "왼쪽부터 1·2·3·4번이다. 1-2-3-4와 역순은 계단, 여러 키를 함께 치며 조합이 교차하는 배치는 데님이다.",
                    Practice = "먼저 한 박씩 1-2-3-4, 쉬고 4-3-2-1. 방향이 바뀌는 첫 자리를 눈으로 짚어라.",
                    Application = "반 박 계단을 오르다 내려온다. 다음은 [1+4]→[2+3]→[1+3]→[2+4]. 대괄호 안 두 키는 함께 쳐라.",
                    Assessment = "정순·역순, 교차 동시치기, 방향 전환을 차례로 본다. 외운 손순서보다 다음 배치를 읽거라.",
                    Observed = "올라갈 때도 내려갈 때도 간격은 같지? 이제 그 길을 네 손으로 걸어 보자.",
                    Praise = "방향이 바뀌어도 길을 잃지 않는구나. 계단·데님 수련을 이수했다.",
                    Missing = "방향 전환이나 동시치기 한쪽을 놓쳤을 수 있다. 두 키가 같은 높이면 함께 치거라.",
                    Timing = "방향이 바뀌어도 박자를 늦추지 마라. 교차 조합은 두 손을 같은 순간에 내려라.",
                    Extra = "익숙한 방향으로 한 타 더 치지 말고, 다음 노트의 자리를 확인해라.",
                });
        }

        private static TrainingLesson Jack(AudioClip audio)
        {
            var imitation = new List<NoteData>();
            Taps(imitation, 2.4, .6, 0, 0);
            Taps(imitation, 6, .6, 3, 3, 3);
            var application = new List<NoteData>();
            Taps(application, 2.4, .3, 1, 1, 1);
            Taps(application, 6, .3, 2, 2);
            Taps(application, 9.6, .3, 0, 0);
            Taps(application, 13.2, .3, 3, 3, 3);
            var assessment = new List<NoteData>();
            Taps(assessment, 2.4, .6, 0, 0);
            Taps(assessment, 6, .6, 3, 3, 3);
            Taps(assessment, 10.8, .3, 1, 1, 1);
            Taps(assessment, 14.4, .3, 2, 2);
            return Lesson("Jack", "jack", "잭·따닥이", "같은 키 2타·3타 · 떼고 다시 누르기 · BPM 100",
                audio, new[] { imitation, application, assessment }, new Teaching
                {
                    Introduction = "문을 두드릴 때도 횟수가 있지. 손가락을 얹어 두기만 해서는 대답이 없다.",
                    Explanation = "같은 키를 이어 치는 것이 잭이다. 두 번은 따닥, 세 번은 따다닥. 매 타격 뒤 키를 떼어야 다음 입력이 된다.",
                    Practice = "1번을 두 번, 쉬고 4번을 세 번. 한 박 간격이니 서두르지 말고 누른 횟수를 세어라.",
                    Application = "반 박 간격으로 줄인다. 2번 세 타, 3번 두 타, 1번 두 타, 4번 세 타다. 떼는 동작도 박자 안에 넣어라.",
                    Assessment = "느린 두 타·세 타 뒤 짧은 반복이다. 키를 누르고 버티거나 마지막 한 타를 덧붙이지 마라.",
                    Observed = "딱 두 번, 딱 세 번. 누르는 힘보다 제때 떼는 손끝이 중요하다.",
                    Praise = "타수도 간격도 분명하구나. 잭·따닥이 수련을 이수했다.",
                    Missing = "키를 계속 누르면 다음 타격은 들어가지 않는다. 매번 떼고 다시 누르며 두 타와 세 타를 세어라.",
                    Timing = "두 번째 타격을 서두르지 마라. 첫 타와 같은 간격으로 이어라.",
                    Extra = "두 타 뒤에는 멈추고, 세 타는 셋에서 끝내라. 타격 횟수까지 기술이다.",
                });
        }

        private static TrainingLesson Burst(AudioClip audio)
        {
            var imitation = new List<NoteData>();
            Taps(imitation, 2.4, .15, 0, 1, 2, 3);
            Taps(imitation, 6, .15, 3, 2, 1, 0);
            var application = new List<NoteData>();
            Taps(application, 2.4, .15, 0, 1, 2, 3, 0, 1, 2, 3);
            Taps(application, 7.2, .15, 3, 2, 1, 0, 3, 2, 1, 0);
            Taps(application, 12, .15, 0, 2, 1, 3, 0, 2, 1, 3, 0, 2, 1, 3);
            var assessment = new List<NoteData>();
            Taps(assessment, 2.4, .15, 0, 1, 2, 3);
            Taps(assessment, 6, .15, 3, 2, 1, 0, 3, 2, 1, 0);
            Taps(assessment, 10.8, .15, 0, 2, 1, 3, 0, 2, 1, 3, 0, 2, 1, 3);
            Taps(assessment, 15.6, .15, 0, 1, 2, 3, 0, 1, 2, 3, 0, 1, 2, 3, 0, 1, 2, 3);
            return Lesson("Burst", "burst", "폭타", "16분음표 · 4→8→12→16타 · BPM 100",
                audio, new[] { imitation, application, assessment }, new Teaching
                {
                    Introduction = "빗줄기가 굵어져도 한 방울씩 떨어진다. 폭타도 한 음씩 정확히 받는 것이다.",
                    Explanation = "폭타는 짧은 간격으로 이어지는 연속 패턴이다. BPM 100의 한 박을 넷으로 나눈 16분음표, 0.15초 간격으로 친다.",
                    Practice = "네 타만 짧게 치고 충분히 쉰다. 1-2-3-4, 다음은 역순이다. 빨리 끝내려 하지 말고 네 타를 고르게 놓아라.",
                    Application = "속도는 그대로, 여덟 타 두 묶음 뒤 열두 타다. 길이가 늘어도 네 타씩 세면 된다.",
                    Assessment = "네 타, 여덟 타, 열두 타, 열여섯 타다. 속도를 올리지는 않는다. 끝 음까지 정확히, 쉼표에는 멈춰라.",
                    Observed = "짧게 쏟고 길게 쉰다. 첫 음과 끝 음을 분명히 하면 중간도 흔들리지 않는다.",
                    Praise = "빗줄기 속에서도 한 음도 흘리지 않았구나. 폭타 수련을 이수했다.",
                    Missing = "긴 묶음의 끝까지 타수를 세어라. 네 타씩 나누어 읽으면 빠진 자리가 보인다.",
                    Timing = "뒤로 갈수록 빨라지지 않게 주의해라. 모든 음의 간격은 0.15초로 같다.",
                    Extra = "묶음이 끝나면 손도 멈춘다. 다음 첫 음이 오기 전에는 기다려라.",
                });
        }

        private static TrainingLesson HoldTap(AudioClip audio)
        {
            var imitation = new List<NoteData>();
            HoldWithTaps(imitation, 0, 2.4, .6, 3, 3, 3);
            HoldWithTaps(imitation, 3, 7.2, .6, 0, 0, 0);
            var application = new List<NoteData>();
            HoldWithTaps(application, 0, 2.4, .3, 2, 3, 2, 3, 2, 3);
            HoldWithTaps(application, 3, 7.2, .3, 1, 0, 1, 0, 1, 0);
            var assessment = new List<NoteData>();
            HoldWithTaps(assessment, 0, 2.4, .6, 3, 2, 3);
            HoldWithTaps(assessment, 3, 7.2, .6, 0, 1, 0);
            HoldWithTaps(assessment, 1, 12, .3, 2, 3, 2, 3, 2, 3);
            HoldWithTaps(assessment, 2, 16.8, .3, 1, 0, 1, 0, 1, 0);
            return Lesson("HoldTap", "hold-tap", "롱잡", "한쪽 유지 + 다른 쪽 타격 · 좌우 반전 · BPM 100",
                audio, new[] { imitation, application, assessment }, new Teaching
                {
                    Introduction = "한 손은 기둥, 한 손은 북채다. 북을 친다고 기둥까지 뽑지는 말거라.",
                    Explanation = "롱잡은 롱노트를 누른 채 다른 레인을 치는 패턴이다. 긴 노트의 꼬리가 판정선에 닿을 때까지 유지하고 그때 뗀다.",
                    Practice = "1번을 유지하며 4번을 세 번 친다. 다음은 좌우 반전이다. 다른 손이 움직여도 홀드 손은 그대로 두어라.",
                    Application = "홀드 반대편의 두 레인을 반 박씩 번갈아 친다. 좌우를 바꿔도 유지하는 손과 타격하는 손의 역할은 같다.",
                    Assessment = "바깥 레인 유지 뒤 안쪽 레인 유지도 살핀다. 다른 타격을 모두 맞혀도 홀드를 일찍 떼면 통과할 수 없다.",
                    Observed = "짧은 음이 끝나도 긴 음은 남아 있지? 꼬리를 확인한 뒤 홀드 손을 놓아라.",
                    Praise = "두 손이 제 역할을 지켰구나. 롱잡 수련을 이수했다.",
                    Missing = "다른 레인 누락뿐 아니라 홀드 조기 해제도 Miss다. 홀드 꼬리가 판정선에 닿을 때까지 유지해라.",
                    Timing = "타격과 해제 모두 박자에 맞춰라. 다른 손의 마지막 타격과 홀드 끝을 혼동하지 마라.",
                    Extra = "홀드 레인을 다시 두드리지 마라. 유지하는 키는 그대로, 움직이는 손만 타격한다.",
                }, HoldEndMode.Release);
        }

        private static TrainingLesson Shield(AudioClip audio)
        {
            var imitation = new List<NoteData>();
            ShieldPhrase(imitation, 0, 3, 2.4, .6);
            ShieldPhrase(imitation, 3, 0, 7.2, .6);
            var application = new List<NoteData>();
            ShieldPhrase(application, 1, 2, 2.4, .3);
            ShieldPhrase(application, 2, 1, 7.2, .3);
            var assessment = new List<NoteData>();
            ShieldPhrase(assessment, 0, 3, 2.4, .6);
            ShieldPhrase(assessment, 3, 0, 7.2, .3);
            ShieldPhrase(assessment, 1, 2, 12, .3);
            ShieldPhrase(assessment, 2, 1, 16.8, .3);
            return Lesson("ShieldRelease", "shield-release", "쉴드·해제", "단타→같은 키 홀드→해제→다른 키 · BPM 100",
                audio, new[] { imitation, application, assessment }, new Teaching
                {
                    Introduction = "방패도 올릴 때와 내릴 때가 있다. 계속 움켜쥐면 다음 동작이 늦는 법이지.",
                    Explanation = "일반 노트 직후 같은 레인의 롱노트를 받는다. 단타 뒤 키를 떼고 다시 눌러 유지한다. 꼬리가 판정선에 닿을 때 정확히 해제한다.",
                    Practice = "1번 단타 뒤 1번 홀드, 해제 뒤 4번이다. 다음은 좌우 반전. 단타를 누른 채 버티면 홀드 시작을 놓친다.",
                    Application = "단타와 홀드 사이, 해제와 다음 입력 사이가 반 박으로 줄어든다. 2번과 3번의 역할을 바꾸어 보자.",
                    Assessment = "네 레인의 단타→홀드→해제→전환을 살핀다. 너무 일찍 떼거나 끝까지 누르고 있어도 해제 판정은 실패한다.",
                    Observed = "누르고, 떼고, 다시 유지하고, 끝에서 놓는다. 해제도 하나의 박자라고 생각해라.",
                    Praise = "막고 푸는 순간이 또렷하구나. 쉴드·해제 수련을 이수했다.",
                    Missing = "홀드 시작 전 재입력과 끝의 해제를 확인해라. 꼬리보다 일찍 떼거나 계속 누르면 Miss다.",
                    Timing = "홀드의 시작만큼 해제도 정확해야 한다. 꼬리를 놓은 뒤 다음 레인으로 옮겨라.",
                    Extra = "단타와 홀드는 각각 한 번 누른다. 홀드 도중 재입력하지 말고 끝에서 해제해라.",
                }, HoldEndMode.Release);
        }

        private static TrainingLesson ChordTrill(AudioClip audio)
        {
            var imitation = new List<NoteData>();
            AlternatingChords(imitation, 2.4, .6, 4);
            AlternatingChords(imitation, 7.2, .6, 4);
            var application = new List<NoteData>();
            AlternatingChords(application, 2.4, .3, 4);
            AlternatingChords(application, 7.2, .3, 8);
            var assessment = new List<NoteData>();
            AlternatingChords(assessment, 2.4, .6, 4);
            AlternatingChords(assessment, 7.2, .3, 4);
            AlternatingChords(assessment, 12, .3, 8);
            return Lesson("ChordTrill", "chord-trill", "동치트릴", "[1+3]↔[2+4] · 한 박→반 박 · BPM 100",
                audio, new[] { imitation, application, assessment }, new Teaching
                {
                    Introduction = "둘이서 한 발자국이다. 한 손이 앞서 나가면 발자국이 두 개가 되겠지.",
                    Explanation = "동치트릴은 [1+3]과 [2+4]를 교대로 치는 패턴이다. 각 조합의 두 키는 함께 누르고, 다음 조합 전에 떼어라.",
                    Practice = "한 박 간격으로 네 조합씩 친다. 1과 3을 함께, 다음은 2와 4를 함께. 두 소리가 하나로 들리게 해라.",
                    Application = "반 박 간격으로 줄여 네 조합, 여덟 조합을 친다. 빨라져도 조합 안의 두 키는 같은 순간이다.",
                    Assessment = "느린 네 조합 뒤 빠른 네 조합과 여덟 조합이다. 각 노트 판정에 더해 두 키의 입력 차이도 50ms 이내여야 한다.",
                    Observed = "두 불빛이 함께 켜졌느냐? 번갈아 치는 단위는 한 키가 아니라 두 키 한 조합이다.",
                    Praise = "두 손이 한 박자로 움직이는구나. 동치트릴 수련을 이수했다.",
                    Missing = "조합의 한쪽 키를 빠뜨리지 않았는지 보아라. [1+3], [2+4]를 한 덩어리씩 읽어라.",
                    Timing = "각 노트의 Perfect 범위와 두 키 사이 50ms를 함께 확인해라. 손 하나가 먼저 떨어지지 않게 맞춰라.",
                    Extra = "네 키를 한꺼번에 누르지 마라. 이번 조합의 두 키만 치고 다음 조합으로 넘어간다.",
                }, maximumChordSpreadSeconds: .05);
        }

        private static void Taps(List<NoteData> notes, double start, double interval, params int[] lanes)
        {
            for (int i = 0; i < lanes.Length; i++)
            {
                notes.Add(NoteData.Tap(lanes[i], start + i * interval));
            }
        }

        private static void Chords(List<NoteData> notes, double start, double interval, params int[][] lanes)
        {
            for (int i = 0; i < lanes.Length; i++)
            {
                foreach (int lane in lanes[i])
                {
                    notes.Add(NoteData.Tap(lane, start + i * interval));
                }
            }
        }

        private static void AlternatingChords(List<NoteData> notes, double start, double interval, int count)
        {
            for (int i = 0; i < count; i++)
            {
                int first = i % 2;
                Chords(notes, start + i * interval, interval, new[] { first, first + 2 });
            }
        }

        private static void HoldWithTaps(List<NoteData> notes, int lane, double start, double interval, params int[] lanes)
        {
            notes.Add(NoteData.Hold(lane, start, start + 2.4));
            Taps(notes, start + .6, interval, lanes);
        }

        private static void ShieldPhrase(List<NoteData> notes, int lane, int next, double start, double interval)
        {
            notes.Add(NoteData.Tap(lane, start));
            double end = start + interval + 1.2;
            notes.Add(NoteData.Hold(lane, start + interval, end));
            notes.Add(NoteData.Tap(next, end + interval));
        }

        private static TrainingLesson Lesson(string name, string id, string title, string description,
            AudioClip audio, List<NoteData>[] charts, Teaching teaching,
            HoldEndMode holdEnd = HoldEndMode.Automatic, double maximumChordSpreadSeconds = 0)
        {
            var lesson = Asset<TrainingLesson>(Folder + name + ".asset", out _);
            var serialized = new SerializedObject(lesson);
            serialized.FindProperty("lessonId").stringValue = id;
            serialized.FindProperty("title").stringValue = title;
            serialized.FindProperty("patternDescription").stringValue = description;
            serialized.FindProperty("definitionVersion").intValue = 1;
            serialized.FindProperty("guided").boolValue = true;
            serialized.FindProperty("stage").enumValueIndex = (int)TrainingStage.Assessment;
            serialized.FindProperty("maximumMisses").intValue = 0;
            serialized.FindProperty("minimumAccuracy").doubleValue = 90;
            serialized.FindProperty("holdEnd").enumValueIndex = (int)holdEnd;
            serialized.FindProperty("maximumChordSpreadSeconds").doubleValue = maximumChordSpreadSeconds;

            string[] fields = { "imitationSong", "applicationSong", "song" };
            string[] names = { "Imitation", "Application", "Assessment" };
            string[] titles = { "따라 하기", "응용", "검정" };
            for (int i = 0; i < charts.Length; i++)
            {
                var notes = charts[i].OrderBy(note => note.StartTime).ToArray();
                double end = notes.Max(note => note.EndTime) + 1.2;
                if (end > audio.length)
                {
                    throw new InvalidOperationException("Training pulse must cover the entire chart: " + name);
                }
                var chart = Asset<RhythmChart>(Folder + name + names[i] + "Chart.asset", out _);
                chart.SetGeneratedContent(notes, end);
                Dirty(chart);
                var song = Asset<SongDefinition>(Folder + name + names[i] + "Song.asset", out _);
                song.SetGeneratedDefaults(id + "-" + names[i], title + " · " + titles[i], audio, chart,
                    SceneResources.Require<GameModeDefinition>("Assets/Game/Settings/FourLaneMode.asset"),
                    new SongTiming { tempoPoints = new[] { new SerializedTempoPoint(0, 100) } });
                Dirty(song);
                serialized.FindProperty(fields[i]).objectReferenceValue = song;
            }

            Dialogue(serialized, name, "introductionDialogue", "Introduction", DialogueExpression.Teaching,
                teaching.Introduction, teaching.Explanation, "네 박 준비음 뒤 내가 먼저 보여주마. 손을 쉬고 노트와 타격 순간을 보거라.");
            Dialogue(serialized, name, "imitationDialogue", "Imitation", DialogueExpression.Teaching, teaching.Practice);
            Dialogue(serialized, name, "applicationDialogue", "Application", DialogueExpression.Confident, teaching.Application);
            Dialogue(serialized, name, "assessmentDialogue", "Assessment", DialogueExpression.Serious,
                teaching.Assessment, "끝까지 연주하고 Miss 0, 정확도 90% 이상, 추가 입력 0회면 통과다. 배운 동작을 차분히 보여라.");
            Dialogue(serialized, name, "demonstrationFinishedDialogue", "Demonstrated", DialogueExpression.Teaching, teaching.Observed);
            Dialogue(serialized, name, "practiceSucceededDialogue", "PracticePassed", DialogueExpression.Praising,
                "좋다. 방금 익힌 동작을 다음 단계에서도 지켜 보거라.");
            Dialogue(serialized, name, "successDialogue", "Passed", DialogueExpression.Praising, teaching.Praise,
                "이수 기록을 남겼다. 수련 선택으로 돌아가거나 검정에 다시 도전해도 좋다.");
            Dialogue(serialized, name, "failureDialogue", "Failed", DialogueExpression.Serious,
                "아직 조건에 닿지 못했구나. 방금 기록을 보고 같은 단계에 다시 도전하자.");
            Dialogue(serialized, name, "missingDialogue", "Missing", DialogueExpression.Teaching,
                "맞히지 못한 노트가 있다. " + teaching.Missing);
            Dialogue(serialized, name, "timingDialogue", "Timing", DialogueExpression.Teaching,
                "타이밍을 살펴보자. " + teaching.Timing);
            Dialogue(serialized, name, "extraInputDialogue", "Extra", DialogueExpression.Teaching,
                "노트에 맞지 않은 입력이 기록되었다. " + teaching.Extra);
            Dialogue(serialized, name, "abortedDialogue", "Aborted", DialogueExpression.Calm,
                "잠시 숨을 고르자. 중단한 단계부터 다시 할 수 있다. 수련 선택으로 돌아가면 시범부터 시작한다.");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Dirty(lesson);
            lesson.Validate();
            return lesson;
        }

        private static void Dialogue(SerializedObject lesson, string name, string field, string suffix,
            DialogueExpression expression, params string[] texts)
        {
            var sequence = Asset<DialogueSequenceAsset>(Folder + "Dialogue/" + name + suffix + ".asset", out _);
            sequence.sequenceId = name + "-" + suffix;
            sequence.version = 1;
            sequence.lines = new DialogueSequenceAsset.Line[texts.Length];
            for (int i = 0; i < texts.Length; i++)
            {
                sequence.lines[i] = new DialogueSequenceAsset.Line
                {
                    id = suffix + "-" + i,
                    text = texts[i],
                    expression = expression,
                    motion = DialogueMotion.Nod,
                };
            }
            sequence.CreateLines();
            Dirty(sequence);
            lesson.FindProperty(field).objectReferenceValue = sequence;
        }
    }
}
