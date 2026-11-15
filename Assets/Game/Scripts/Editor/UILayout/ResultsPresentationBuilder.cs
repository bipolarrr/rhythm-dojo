using System.Collections.Generic;
using RhythmDojo.Audio;
using RhythmDojo.UI;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.EditorTools
{
    public static class ResultsPresentationBuilder
    {
        public static void Build(ResultsView view, RectTransform backdrop, RectTransform report,
            RectTransform summary, Text heading, RectTransform seal, Text resultCaption, Text help,
            RectTransform divider)
        {
            var presentation = view.gameObject.AddComponent<ResultsPresentation>();
            view.presentation = presentation;
            presentation.inputGate = view.gameObject.AddComponent<CanvasGroup>();
            presentation.scoreValue = view.scoreValue;
            presentation.navigationHelp = help;
            presentation.seal = seal;
            presentation.sounds = BuildAudio(view.transform);

            var tracks = new List<ResultsPresentation.Track>();
            Add(tracks, backdrop, 0, .36f, Vector2.zero, new Vector2(1.025f, 1.025f));
            Add(tracks, report, .05f, .40f, new Vector2(0, -16), new Vector2(1, .96f));
            Add(tracks, heading.rectTransform, .20f, .36f, new Vector2(-10, 0), Vector2.one, true, true);
            Add(tracks, summary, .32f, .32f, new Vector2(0, -9), Vector2.one);
            Add(tracks, view.title.rectTransform, .37f, .34f, new Vector2(-8, 0), Vector2.one, false, true);
            Add(tracks, view.feedback, .42f, .28f, Vector2.zero, Vector2.one);
            Add(tracks, divider, .38f, .33f, Vector2.zero, new Vector2(.02f, 1));
            Add(tracks, view.scoreValue.rectTransform, ResultsPresentation.CountStart, .27f,
                new Vector2(0, -14), new Vector2(.94f, .94f), true);
            Add(tracks, view.perfect.transform.parent as RectTransform, .90f, .22f,
                new Vector2(18, 0), Vector2.one, true);
            Add(tracks, view.good.transform.parent as RectTransform, 1.04f, .22f,
                new Vector2(18, 0), Vector2.one, true);
            Add(tracks, view.miss.transform.parent as RectTransform, 1.18f, .22f,
                new Vector2(18, 0), Vector2.one, true);
            Add(tracks, view.accuracy.rectTransform, 1.32f, .25f, new Vector2(0, -9), Vector2.one);
            Add(tracks, view.maxCombo.rectTransform, 1.45f, .25f, new Vector2(0, -9), Vector2.one);
            Add(tracks, resultCaption.rectTransform, .42f, .26f, Vector2.zero, Vector2.one);
            Add(tracks, view.outcome.rectTransform, .46f, .37f, Vector2.zero,
                new Vector2(1.06f, 1.06f), false, true);
            Add(tracks, view.progressNote.rectTransform, .58f, .28f, new Vector2(0, -6), Vector2.one);
            Add(tracks, seal, ResultsPresentation.StampTime, .27f,
                Vector2.zero, new Vector2(1.24f, 1.24f), true);
            Add(tracks, view.newRecordBadge.transform as RectTransform, ResultsPresentation.StampTime, .26f,
                Vector2.zero, new Vector2(1.3f, 1.3f), true);
            Add(tracks, view.scoreSummary.rectTransform, 1.62f, .24f, new Vector2(12, 0), Vector2.one);
            Add(tracks, view.retryButton.transform as RectTransform, 1.72f, .26f,
                new Vector2(0, -8), Vector2.one, true);
            Add(tracks, view.recommendationButton.transform as RectTransform, 1.78f, .24f,
                new Vector2(0, -6), Vector2.one);
            Add(tracks, view.moreButton.transform as RectTransform, 1.78f, .24f,
                new Vector2(10, 0), Vector2.one);

            presentation.tracks = tracks.ToArray();
            presentation.Validate();
        }

        private static void Add(List<ResultsPresentation.Track> tracks, RectTransform rect,
            float delay, float duration, Vector2 offset, Vector2 scale, bool overshoot = false, bool brush = false)
        {
            var track = new ResultsPresentation.Track
            {
                rect = rect,
                group = rect.gameObject.AddComponent<CanvasGroup>(),
                delay = delay,
                duration = duration,
                offset = offset,
                scale = scale,
                overshoot = overshoot
            };
            if (brush)
            {
                track.brush = rect.gameObject.AddComponent<ResultsBrushReveal>();
            }
            tracks.Add(track);
        }

        private static ResultsSoundPlayer BuildAudio(Transform parent)
        {
            var root = new GameObject("Results Foley");
            root.SetActive(false);
            root.transform.SetParent(parent, false);
            var sounds = root.AddComponent<ResultsSoundPlayer>();
            sounds.voices = new AudioSource[3];
            for (int i = 0; i < sounds.voices.Length; i++)
            {
                var source = new GameObject("Results Voice " + i).AddComponent<AudioSource>();
                source.transform.SetParent(root.transform, false);
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0;
                source.priority = 120;
                sounds.voices[i] = source;
            }
            sounds.arrival = Clip("Arrival");
            sounds.brush = Clip("Brush");
            sounds.counts = new[] { Clip("Count1"), Clip("Count2"), Clip("Count3") };
            sounds.statistic = Clip("Statistic");
            sounds.complete = Clip("Clear");
            sounds.failed = Clip("Failed");
            sounds.menu = SceneResources.Require<AudioClip>("Assets/Game/Resources/UI/DojoClick.wav");
            sounds.Validate();
            root.SetActive(true);
            return sounds;
        }

        private static AudioClip Clip(string name)
        {
            return SceneResources.Require<AudioClip>("Assets/Game/Audio/Foley/Results" + name + ".wav");
        }
    }
}
