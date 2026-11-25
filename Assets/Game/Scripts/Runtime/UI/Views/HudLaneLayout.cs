using System;
using System.Collections.Generic;
using RhythmDojo.Gameplay;
using RhythmDojo.Services;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.UI
{
    public sealed class HudLaneLayout : MonoBehaviour
    {
        public RectTransform laneRoot;
        public Image template;
        [SerializeField] private List<Image> keys = new List<Image>();

        public void Validate()
        {
            if (!laneRoot || !template)
            {
                throw new InvalidOperationException("HUD key legend missing.");
            }
        }

        public Image[] Build(GameModeDefinition mode, GameplayPresentationSettings presentation,
            string modeId = null)
        {
            Validate();
            var indicators = new Image[mode.LaneCount];
            for (int i = 0; i < mode.LaneCount; i++)
            {
                if (i == keys.Count)
                {
                    keys.Add(Instantiate(template, laneRoot));
                }

                var image = keys[i];
                image.gameObject.SetActive(true);
                image.name = $"Lane {i} Feedback";
                var point = GameplayViewport.Position(i, mode.LaneCount, 0);
                image.rectTransform.anchoredPosition = new Vector2(point.x, 8);
                string label = mode.GetLane(i).displayName;
                if (UnityEngine.Application.isPlaying)
                {
                    label = PlayKeyBindings.GetLabel(mode, modeId ?? mode.name, i);
                }
                image.GetComponentInChildren<Text>().text = label;
                indicators[i] = image;
            }

            for (int i = indicators.Length; i < keys.Count; i++)
            {
                keys[i].gameObject.SetActive(false);
            }
            return indicators;
        }
    }
}
