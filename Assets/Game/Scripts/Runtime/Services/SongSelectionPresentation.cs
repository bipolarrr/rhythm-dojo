using System;
using System.Linq;
using RhythmDojo.Application;
using RhythmDojo.Content;
using RhythmDojo.UI;
using RhythmDojo.Gameplay;
using UnityEngine;

namespace RhythmDojo.Services
{
    public sealed class SongSelectionPresentation
    {
        public string Title { get; private set; }
        public string Artist { get; private set; }
        public string Metadata { get; private set; }
        public string Details { get; private set; }
        public string Record { get; private set; }
        public string ComboRecord { get; private set; }
        public string Message { get; private set; }
        public string Heading { get; private set; }
        public Sprite Cover { get; private set; }
        public bool CanPlay { get; private set; }
        public bool[] Difficulties { get; private set; }
        public int SelectedDifficulty { get; private set; }
        public int Index { get; private set; }
        public int Count { get; private set; }

        public static SongSelectionPresentation Create(ISongSelectionService selection,
            SongSelectionList list, DifficultyProfile[] difficultyOptions, ISelectionPreferences preferences,
            IPlayRulesService rules, bool hard, Sprite fallbackCover)
        {
            var result = new SongSelectionPresentation
            {
                Title = "곡을 선택하세요",
                Artist = "",
                Metadata = "",
                Details = "",
                Record = "스코어  —\n랭크  —\n레이트  —",
                ComboRecord = "최대 콤보  —\n기록 없음",
                Cover = fallbackCover,
                Index = list.IndexOf(selection.SelectedSongId),
                Count = list.Entries.Count,
                SelectedDifficulty = Array.IndexOf(difficultyOptions, selection.SelectedDifficulty),
                Difficulties = new bool[difficultyOptions.Length],
                Heading = "자유 연습"
            };
            if (rules != null)
            {
                hard = rules.Snapshot.Mode == RhythmDojo.Gameplay.PlayMode.Challenge;
            }
            if (hard)
            {
                result.Heading = "실력 검정";
            }
            if (result.Index < 0)
            {
                result.Message = "등록된 곡이 없습니다.";
                if (list.FavoritesOnly)
                {
                    result.Message = "즐겨찾기한 곡이 없습니다.";
                }
                return result;
            }

            var song = list.Entries[result.Index];
            var chart = song.Charts.FirstOrDefault(item => (int)item.Slot == result.SelectedDifficulty);
            result.Title = song.Title;
            result.Artist = song.Artist;
            if (song.Cover)
            {
                result.Cover = song.Cover;
            }
            string bpm = $"{song.MinBpm:0.##}";
            if (song.MinBpm != song.MaxBpm)
            {
                bpm += $"–{song.MaxBpm:0.##}";
            }
            var time = TimeSpan.FromSeconds(Math.Max(0, chart?.Duration ?? song.Duration));
            result.Metadata = $"{song.Artist}\nBPM {bpm} · {(int)time.TotalMinutes}:{time.Seconds:00}";
            result.Details = $"{chart?.NoteCount ?? song.NoteCount} 노트 · {song.LaneCount} 레인";
            for (int i = 0; i < result.Difficulties.Length; i++)
            {
                result.Difficulties[i] = song.Charts.Any(item => (int)item.Slot == i);
            }
            result.CanPlay = song.Error == null && chart != null;
            if (song.Error != null || chart?.Error != null)
            {
                result.CanPlay = false;
                var failure = new SongLoadErrorDisplay(chart?.Failure ?? song.Failure);
                result.Message = failure.Message;
            }
            else if (chart == null)
            {
                result.Message = "선택한 난이도의 채보가 없습니다.";
            }

            string recordKey = selection.SelectedDifficulty.name;
            if (chart != null && rules != null)
            {
                recordKey = RecordIdentity.Rules(chart.Id, chart.Revision, rules.Snapshot);
            }
            var best = preferences.FindRecord(song.Id, recordKey);
            if (best != null)
            {
                result.Record = $"스코어  {best.score:N0}\n랭크  {best.Rank}\n레이트  {best.rate:0.00}%";
                string clearLabel = "Gauge —";
                if (best.gaugeClear)
                {
                    clearLabel = "Gauge CLEAR";
                }
                result.ComboRecord = $"최대 콤보  {best.maxCombo:N0}\n{clearLabel}\nChallenge "
                    + string.Join(" / ", Enumerable.Range(1, 3).Select(i =>
                    {
                        if ((best.challengeAchievements & (1 << (i - 1))) != 0)
                        {
                            return i.ToString();
                        }
                        return "—";
                    }));
            }
            if (preferences is SelectionPreferences records)
            {
                var failed = records.FindFailedRecord(song.Id, recordKey);
                if (failed != null)
                {
                    result.ComboRecord += $"\n실패 완주 {failed.score:N0} · {failed.rate:0.00}%";
                }
            }
            return result;
        }
    }
}
