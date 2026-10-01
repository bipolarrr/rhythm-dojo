namespace RhythmDojo.Services
{
    public interface IMainMenuActions
    {
        bool Transitioning { get; }
        void StartGame();
        void QuitGame();
    }
}