using System;
using UnityEngine;

namespace RhythmDojo.Gameplay
{
    [Serializable]
    public struct LaneDefinition
    {
        public string binding;
        public string displayName;
        public Color color;
        public LaneDefinition(string binding, string displayName, Color color)
        { this.binding = binding; this.displayName = displayName; this.color = color; }
    }
    [CreateAssetMenu(menuName = "Rhythm Dojo/Game Mode")]
    public sealed class GameModeDefinition : ScriptableObject
    {
        [SerializeField] private LaneDefinition[] lanes;
        [SerializeField] private string startBinding = "<Keyboard>/space";
        [SerializeField] private string returnBinding = "<Keyboard>/escape";
        [SerializeField] private float laneSpacing = 1.4f;
        [SerializeField] private float hudLaneSpacing = 100;
        public int LaneCount => lanes?.Length ?? 0;
        public LaneDefinition GetLane(int index) => lanes[index];
        public string StartBinding => startBinding;
        public string ReturnBinding => returnBinding;
        public float LaneSpacing => laneSpacing;
        public float HudLaneSpacing => hudLaneSpacing;
        public float LaneX(int lane) => CenteredPosition(lane, LaneCount, laneSpacing);
        public static float CenteredPosition(int index, int count, float spacing) => (index - (count - 1) * .5f) * spacing;
        public GameModeRules ToRules() { Validate(); return new GameModeRules(LaneCount); }
        public void Validate()
        {
            if (LaneCount <= 0 || !float.IsFinite(laneSpacing) || laneSpacing <= 0 ||
                !float.IsFinite(hudLaneSpacing) || hudLaneSpacing <= 0 ||
                string.IsNullOrWhiteSpace(startBinding) || string.IsNullOrWhiteSpace(returnBinding))
                throw new InvalidOperationException("Invalid lane mode layout or command bindings.");
            var bindings = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { startBinding, returnBinding };
            if (bindings.Count != 2) throw new InvalidOperationException("Start and return bindings must differ.");
            foreach (var lane in lanes)
                if (string.IsNullOrWhiteSpace(lane.binding) || string.IsNullOrWhiteSpace(lane.displayName) ||
                    !bindings.Add(lane.binding)) throw new InvalidOperationException("Lane bindings must be distinct and named.");
        }
        public void SetGeneratedDefaults(LaneDefinition[] definitions) { lanes = (LaneDefinition[])definitions.Clone(); }
    }
}
