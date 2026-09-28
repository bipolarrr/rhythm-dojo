using System;
using System.Threading;
using System.Threading.Tasks;
using RhythmDojo.Application;
using RhythmDojo.Content;
using RhythmDojo.Gameplay;

namespace RhythmDojo.Services
{
    public interface IAppNavigation
    {
        bool Transitioning { get; }
        void ShowSelection();
        void ShowSettings();
        Task PlaySelectedAsync(CancellationToken cancellationToken);
    }
    public interface ISongSelectionService
    {
        string SelectedSongId { get; }
        DifficultyProfile SelectedDifficulty { get; }
        ScrollMode SelectedScrollMode { get; }
        double SelectedMultiplier { get; }
        ISongLibrary Library { get; }
        void UpdateSelection(string songId, DifficultyProfile difficulty, ScrollMode mode, double multiplier);
    }
    public readonly struct AudioState
    {
        public readonly int BufferSize, BufferCount, SampleRate;
        public AudioState(int size, int count, int rate) { BufferSize = size; BufferCount = count; SampleRate = rate; }
    }
    public interface IAudioSettingsService
    {
        int OptionCount { get; }
        int DefaultBufferSize { get; }
        int SelectedBufferOption { get; }
        int RequestedBufferSize { get; }
        string BufferStatus { get; }
        int GetBufferSize(int option);
        AudioState Actual { get; }
        event Action Changed;
        void SelectBufferOption(int option);
        bool ApplyBufferSize();
    }
    public interface IEditorPlaytestService
    {
        Task PlayAsync(SongDocument document, ISongDocumentLoader loader, string editorSessionId,
            string returnScenePath, Action<string> returned, CancellationToken cancellationToken);
    }
}
