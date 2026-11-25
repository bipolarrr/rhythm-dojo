using System;
using System.Collections.Generic;
using RhythmDojo.Application;
using RhythmDojo.Gameplay;
using RhythmDojo.UI;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.Presentation
{
    public sealed class PlayfieldPresenter : MonoBehaviour
    {
        private const int BeatsPerMeasure = 4;
        private const int MaxVisibleMeasureLines = 128;
        public GameplayViewport viewport;
        public RectTransform grid;
        public Image judgmentLine;
        [SerializeField] private List<Image> lanes = new List<Image>();
        [SerializeField] private List<Image> rails = new List<Image>();
        [SerializeField] private List<Image> shoes = new List<Image>();

        private readonly List<Image> measureLines = new List<Image>();
        private TempoMap measureTempo;
        private IScrollTimeline measureScroll;
        private double lastMeasureBeat;
        private GameplayPresentationSettings settings;
        private double[] flashUntil;
        private float width;
        private float opacity = 1;

        public void Validate()
        {
            if (!viewport || !grid || !judgmentLine)
            {
                throw new InvalidOperationException("Missing authored playfield.");
            }
        }

        public void Build(GameModeDefinition mode, GameplayPresentationSettings settings)
        {
            Validate();
            mode.Validate();
            settings.Validate();
            if (!settings.fieldTemplate || !settings.highway || !settings.divider)
            {
                throw new InvalidOperationException("Missing lane surface artwork.");
            }
            if (!settings.border || !settings.strikeShoe || !settings.judgmentLine)
            {
                throw new InvalidOperationException("Missing strike or border artwork.");
            }
            if (!settings.pressedShoe)
            {
                throw new InvalidOperationException("Missing pressed receptor artwork.");
            }
            if (!settings.beat || !settings.measure)
            {
                throw new InvalidOperationException("Missing timing grid artwork.");
            }

            this.settings = settings;
            int count = mode.LaneCount;
            viewport.Fit(count);
            width = count * GameplayViewport.LaneWidth;
            Resize(lanes, count);
            Resize(rails, count + 1);
            Resize(shoes, count);
            flashUntil = new double[count];

            for (int i = 0; i < count; i++)
            {
                var point = GameplayViewport.Position(i, count, 0);
                Place(lanes[i], $"Lane {i}", settings.highway, point.x, GameplayViewport.Height / 2);
                lanes[i].transform.SetAsFirstSibling();
                Place(shoes[i], $"Strike Shoe {i}", settings.strikeShoe, point.x, point.y - 6);
                shoes[i].transform.SetAsLastSibling();
            }

            for (int i = 0; i <= count; i++)
            {
                var sprite = settings.divider;
                if (i == 0 || i == count)
                {
                    sprite = settings.border;
                }
                Place(rails[i], $"Rail {i}", sprite, i * GameplayViewport.LaneWidth - width / 2,
                    GameplayViewport.Height / 2);
                rails[i].transform.SetSiblingIndex(lanes.Count + i);
            }

            Place(judgmentLine, "Judgement Line", settings.judgmentLine, 0, GameplayViewport.StrikeY);
            judgmentLine.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            judgmentLine.transform.SetAsLastSibling();
            SetRailOpacity(opacity);
            measureTempo = null;
            RenderMeasureLines(SessionState.Ready, 0);
        }

        private void Resize(List<Image> items, int count)
        {
            // Only lane-count changes require additional instances of the authored image.
            while (items.Count < count)
            {
                items.Add(Instantiate(settings.fieldTemplate, transform));
            }
            for (int i = 0; i < items.Count; i++)
            {
                items[i].gameObject.SetActive(i < count);
            }
        }

        private static void Place(Image image, string name, Sprite sprite, float x, float y)
        {
            image.name = name;
            image.sprite = sprite;
            image.rectTransform.anchoredPosition = new Vector2(x, y);
            image.SetNativeSize();
        }

        public void SetRailOpacity(float opacity)
        {
            this.opacity = Mathf.Clamp01(opacity);
            var tint = new Color(1, 1, 1, this.opacity);
            foreach (var lane in lanes)
            {
                lane.color = tint;
            }
            foreach (var rail in rails)
            {
                rail.color = tint;
            }
            foreach (var line in measureLines)
            {
                line.color = tint;
            }
        }

        public void FlashInput(int lane)
        {
            flashUntil[lane] = Time.unscaledTimeAsDouble + .09;
        }

        public void RenderInputs(IGameplayReadModel model)
        {
            for (int i = 0; i < flashUntil.Length; i++)
            {
                var sprite = settings.strikeShoe;
                if (model.IsHeld(i) || flashUntil[i] > Time.unscaledTimeAsDouble)
                {
                    sprite = settings.pressedShoe;
                }
                shoes[i].sprite = sprite;
            }
        }

        public void ConfigureMeasureLines(TempoMap tempo, IScrollTimeline scroll, ChartData chart)
        {
            ConfigureMeasureLines(tempo, scroll, chart.CompletionTime);
        }

        public void ConfigureMeasureLines(TempoMap tempo, IScrollTimeline scroll, double completionTime)
        {
            measureTempo = tempo;
            measureScroll = scroll;
            lastMeasureBeat = tempo.BeatAtTime(completionTime);
        }

        public void RenderMeasureLines(SessionState state, double songTime)
        {
            int visible = 0;
            if (measureTempo != null && state == SessionState.Playing)
            {
                double beat = Math.Max(0, Math.Ceiling(measureTempo.BeatAtTime(songTime)));
                while (beat <= lastMeasureBeat && visible < MaxVisibleMeasureLines)
                {
                    double time = measureTempo.TimeAtBeat(beat);
                    double distance = measureScroll.DistanceBetween(songTime, time);
                    float y = GameplayViewport.Position(0, 1, distance).y;
                    if (y > GameplayViewport.Height)
                    {
                        break;
                    }

                    if (visible == measureLines.Count)
                    {
                        var image = Instantiate(settings.fieldTemplate, grid);
                        image.type = Image.Type.Tiled;
                        image.color = new Color(1, 1, 1, opacity);
                        measureLines.Add(image);
                    }
                    var line = measureLines[visible];
                    line.sprite = settings.beat;
                    if (beat % BeatsPerMeasure == 0)
                    {
                        line.sprite = settings.measure;
                    }
                    line.rectTransform.sizeDelta = new Vector2(width, 1);
                    line.rectTransform.anchoredPosition = new Vector2(0, y);
                    line.gameObject.SetActive(true);
                    visible++;
                    beat++;
                }
            }

            for (int i = visible; i < measureLines.Count; i++)
            {
                measureLines[i].gameObject.SetActive(false);
            }
        }
    }
}
