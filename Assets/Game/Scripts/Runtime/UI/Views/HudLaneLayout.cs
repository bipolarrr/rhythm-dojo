using System;
using UnityEngine;
using UnityEngine.UI;
using RhythmDojo.Gameplay;
namespace RhythmDojo.UI
{
    public sealed class HudLaneLayout : MonoBehaviour
    {
        public RectTransform laneRoot;
        public void Validate() { if (!laneRoot) throw new InvalidOperationException("HUD lane root missing."); }
        public Image[] Build(GameModeDefinition mode, GameplayPresentationSettings presentation)
        {
            for (int i = laneRoot.childCount - 1; i >= 0; i--)
            {
                var go = laneRoot.GetChild(i).gameObject; go.SetActive(false);
                if (UnityEngine.Application.isPlaying) Destroy(go); else DestroyImmediate(go);
            }
            var indicators = new Image[mode.LaneCount];
            for (int i = 0; i < mode.LaneCount; i++)
            {
                var go = new GameObject($"Lane {i} Feedback", typeof(RectTransform)); go.transform.SetParent(laneRoot, false);
                indicators[i] = go.AddComponent<Image>(); indicators[i].color = presentation.idleColor;
                var rect = (RectTransform)go.transform; rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, 0);
                rect.anchoredPosition = new Vector2(GameModeDefinition.CenteredPosition(i, mode.LaneCount, mode.HudLaneSpacing), 28);
                rect.sizeDelta = new Vector2(mode.HudLaneSpacing * .88f, 48);
                var key = UiElements.Label("Key", go.transform, mode.GetLane(i).displayName, Vector2.zero, rect.sizeDelta, 26);
                key.alignment = TextAnchor.MiddleCenter;
            }
            return indicators;
        }
    }
}
