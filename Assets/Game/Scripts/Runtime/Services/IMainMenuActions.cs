namespace RhythmDojo.Services
{
    public interface IMainMenuActions
    {
        bool Transitioning { get; }

        void StartGame();
        void ShowSettings();
        void ShowChartEditor();
        void ShowChartWorkspace();
        void QuitGame();
    }
}
