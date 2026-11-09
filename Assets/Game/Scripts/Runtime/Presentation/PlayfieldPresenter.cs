using System;
using System.Collections.Generic;
using RhythmDojo.Application;
using RhythmDojo.Gameplay;
using UnityEngine;
using UnityEngine.Rendering;

namespace RhythmDojo.Presentation
{
    public sealed class PlayfieldPresenter : MonoBehaviour
    {
        private const int BeatsPerMeasure = 4;
        private const int MaxVisibleMeasureLines = 128;

        [SerializeField]
        private Material highwayMaterial;

        [SerializeField]
        private Material railMaterial;

        [SerializeField]
        private Material judgmentMaterial;

        [SerializeField]
        private Material fadeMaterial;

        private readonly List<Renderer> lanes = new List<Renderer>();
        private readonly List<Renderer> rails = new List<Renderer>();
        private Material fadedHighway;
        private Material fadedRails;

        private readonly List<Transform> measureLines = new List<Transform>();
        private TempoMap measureTempo;
        private IScrollTimeline measureScroll;
        private double lastMeasureBeat;
        private float highwayEnd;
        private Vector3 measureLineScale;

        public void Validate()
        {
            if (!highwayMaterial || !railMaterial || !judgmentMaterial)
            {
                throw new System.InvalidOperationException("Playfield materials missing.");
            }
        }

        public void Build(GameModeDefinition mode, GameplayPresentationSettings settings)
        {
            Validate();
            mode.Validate();
            settings.Validate();

            if (settings.laneGap >= mode.LaneSpacing)
            {
                throw new System.InvalidOperationException(
                    "Lane gap must be smaller than spacing."
                );
            }

            lanes.Clear();
            rails.Clear();
            measureLines.Clear();
            measureTempo = null;
            highwayEnd = settings.highwayCenter + settings.highwayLength * .5f;
            measureLineScale = new Vector3(
                mode.LaneCount * mode.LaneSpacing,
                settings.judgmentLineHeight * .35f,
                settings.judgmentLineDepth * .55f
            );

            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
                child.SetActive(false);

                if (UnityEngine.Application.isPlaying)
                {
                    Destroy(child);
                }
                else
                {
                    DestroyImmediate(child);
                }
            }

            for (int i = 0; i < mode.LaneCount; i++)
            {
                var renderer = Box(
                    $"Lane {i}",
                    new Vector3(mode.LaneX(i), -.15f, settings.highwayCenter),
                    new Vector3(mode.LaneSpacing - settings.laneGap, .2f, settings.highwayLength),
                    highwayMaterial
                );
                lanes.Add(renderer);
            }

            for (int i = 0; i <= mode.LaneCount; i++)
            {
                float width = settings.railWidth;
                if (i > 0 && i * 2 == mode.LaneCount)
                {
                    width *= 2;
                }

                var renderer = Box(
                    $"Rail {i}",
                    new Vector3(
                        (i - mode.LaneCount * .5f) * mode.LaneSpacing,
                        0,
                        settings.highwayCenter
                    ),
                    new Vector3(width, settings.railHeight, settings.highwayLength),
                    railMaterial
                );
                rails.Add(renderer);
            }

            Box(
                "Judgement Line",
                new Vector3(0, .05f, 0),
                new Vector3(
                    mode.LaneCount * mode.LaneSpacing,
                    settings.judgmentLineHeight,
                    settings.judgmentLineDepth
                ),
                judgmentMaterial
            );
        }

        public void SetRailOpacity(float opacity)
        {
            opacity = Mathf.Clamp01(opacity);
            var laneSurface = highwayMaterial;
            var railSurface = railMaterial;

            if (opacity > 0 && opacity < 1)
            {
                fadedHighway ??= new Material(fadeMaterial);
                fadedRails ??= new Material(fadeMaterial)
                {
                    renderQueue = fadeMaterial.renderQueue + 1,
                };

                SetAlpha(fadedHighway, highwayMaterial, opacity);
                SetAlpha(fadedRails, railMaterial, opacity);
                laneSurface = fadedHighway;
                railSurface = fadedRails;
            }

            SetSurface(lanes, laneSurface, opacity > 0);
            SetSurface(rails, railSurface, opacity > 0);
        }

        private static void SetAlpha(Material material, Material source, float opacity)
        {
            var color = source.GetColor("_BaseColor");
            color.a *= opacity;
            material.SetColor("_BaseColor", color);
        }

        private static void SetSurface(List<Renderer> renderers, Material material, bool visible)
        {
            foreach (var renderer in renderers)
            {
                renderer.sharedMaterial = material;
                renderer.enabled = visible;
            }
        }

        public void ConfigureMeasureLines(TempoMap tempo, IScrollTimeline scroll, ChartData chart)
        {
            measureTempo = tempo;
            measureScroll = scroll;
            lastMeasureBeat = tempo.BeatAtTime(chart.CompletionTime);
        }

        public void RenderMeasureLines(SessionState state, double songTime)
        {
            int visible = 0;

            if (measureTempo != null && state == SessionState.Playing)
            {
                double beat = measureTempo.BeatAtTime(songTime);
                beat = Math.Max(0, Math.Ceiling(beat / BeatsPerMeasure)) * BeatsPerMeasure;

                while (beat <= lastMeasureBeat && visible < MaxVisibleMeasureLines)
                {
                    double time = measureTempo.TimeAtBeat(beat);
                    double distance = measureScroll.DistanceBetween(songTime, time);

                    if (distance > highwayEnd)
                    {
                        break;
                    }

                    if (visible == measureLines.Count)
                    {
                        var renderer = Box("Measure Line", Vector3.zero, measureLineScale, judgmentMaterial);
                        renderer.shadowCastingMode = ShadowCastingMode.Off;
                        measureLines.Add(renderer.transform);
                    }

                    var line = measureLines[visible];
                    line.position = new Vector3(0, .035f, (float)distance);
                    line.gameObject.SetActive(true);

                    visible++;
                    beat += BeatsPerMeasure;
                }
            }

            for (int i = visible; i < measureLines.Count; i++)
            {
                measureLines[i].gameObject.SetActive(false);
            }
        }

        private Renderer Box(string name, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(transform);
            go.transform.position = position;
            go.transform.localScale = scale;

            var collider = go.GetComponent<Collider>();
            collider.enabled = false;

            if (UnityEngine.Application.isPlaying)
            {
                Destroy(collider);
            }
            else
            {
                DestroyImmediate(collider);
            }

            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            return renderer;
        }

        private void OnDestroy()
        {
            if (fadedHighway)
            {
                Destroy(fadedHighway);
            }

            if (fadedRails)
            {
                Destroy(fadedRails);
            }
        }
    }
}
