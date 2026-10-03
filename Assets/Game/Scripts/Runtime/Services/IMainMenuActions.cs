namespace RhythmDojo.Services
{
    public interface IMainMenuActions
    {
        bool Transitioning { get; }
        void StartGame();
        void ShowSettings();
        void QuitGame();
    }
}
