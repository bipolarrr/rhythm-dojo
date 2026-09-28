using UnityEngine;

namespace RhythmDojo.Gameplay
{
    [CreateAssetMenu(menuName = "Rhythm Dojo/Difficulty Profile")]
    public sealed class DifficultyProfile : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private double perfectWindow;
        [SerializeField] private double goodWindow;
        public string Id => id;
        public string DisplayName => displayName;
        public JudgmentSettings ToSettings() => new JudgmentSettings(perfectWindow, goodWindow);
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(displayName))
                throw new System.InvalidOperationException("Difficulty needs an ID and display name.");
            _ = ToSettings();
        }
        public void SetGeneratedDefaults(string identifier, string label, double perfect, double good)
        { id = identifier; displayName = label; perfectWindow = perfect; goodWindow = good; }
    }
}
