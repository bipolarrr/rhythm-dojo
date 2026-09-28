using System;
using UnityEngine;
using RhythmDojo.Application;
using RhythmDojo.Gameplay;

namespace RhythmDojo.Presentation
{
    public sealed class NotePresenter : MonoBehaviour, INotePresenter
    {
        [SerializeField] private NoteView tapTemplate;
        [SerializeField] private NoteView holdTemplate;
        [SerializeField] private Material noteMaterial;
        [SerializeField] private NoteView[] views;
        private Material[] laneMaterials;
        private GameModeDefinition mode;
        private GameplayPresentationSettings settings;
        private ChartData chart;
        private IScrollTimeline scroll;
        public void Configure(GameModeDefinition definition, GameplayPresentationSettings presentation)
        { mode = definition; settings = presentation; Validate(); }
        public void Validate(ChartData generatedChart = null)
        {
            if (!tapTemplate || !holdTemplate || !noteMaterial)
                throw new InvalidOperationException($"NotePresenter dependencies: tap={tapTemplate}, hold={holdTemplate}, material={noteMaterial}.");
            tapTemplate.Validate(NoteKind.Tap); holdTemplate.Validate(NoteKind.Hold);
            if (generatedChart == null) return;
            if (views == null || views.Length != generatedChart.Count)
                throw new InvalidOperationException("Generated note view count does not match the chart.");
            for (int i = 0; i < views.Length; i++)
            {
                if (!views[i]) throw new InvalidOperationException($"Generated note view {i} missing.");
                for (int previous = 0; previous < i; previous++)
                    if (views[previous] == views[i]) throw new InvalidOperationException($"Duplicate generated note view {i}.");
                views[i].Validate(generatedChart[i].Kind);
            }
        }
        public void Initialize(ChartData chart, GameModeRules rules, IScrollTimeline scroll)
        {
            Validate();
            if (!mode || !settings || mode.LaneCount != rules.LaneCount)
                throw new InvalidOperationException($"NotePresenter mode={mode}, settings={settings}, lanes={mode?.LaneCount}, rules={rules.LaneCount}.");
            this.chart = chart; this.scroll = scroll;
            ClearMaterials(); laneMaterials = new Material[mode.LaneCount];
            for (int lane = 0; lane < laneMaterials.Length; lane++)
            {
                laneMaterials[lane] = new Material(noteMaterial);
                laneMaterials[lane].SetColor("_BaseColor", mode.GetLane(lane).color);
            }
            bool reusable = views != null && views.Length == chart.Count;
            if (reusable)
                for (int i = 0; i < views.Length; i++)
                    reusable &= views[i] && views[i].HasBody == (chart[i].Kind == NoteKind.Hold);
            if (!reusable)
            {
                ClearViews(); views = new NoteView[chart.Count];
                for (int i = 0; i < chart.Count; i++)
                    views[i] = Instantiate(chart[i].Kind == NoteKind.Hold ? holdTemplate : tapTemplate, transform);
            }
            for (int i = 0; i < views.Length; i++)
            {
                views[i].name = $"Note {i:00} - {chart[i].Kind} - Lane {chart[i].Lane}";
                views[i].SetMaterial(laneMaterials[chart[i].Lane]);
                views[i].Render(chart[i], NoteState.Pending, 0, scroll, mode, settings);
            }
        }
        public void Render(RhythmSession session, double songTime)
        {
            if (views == null) return;
            for (int i = 0; i < views.Length; i++) views[i].Render(chart[i], session.GetState(i), songTime, scroll, mode, settings);
        }
        public void Reset() { }
        public void Clear() { ClearViews(); ClearMaterials(); }
        private void ClearViews()
        {
            if (views != null) foreach (var view in views) if (view) Remove(view.gameObject);
            views = null;
        }
        private void ClearMaterials()
        {
            if (laneMaterials != null) foreach (var material in laneMaterials) if (material) Remove(material);
            laneMaterials = null;
        }
        private static void Remove(UnityEngine.Object value)
        { if (UnityEngine.Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
        private void OnDestroy() => ClearMaterials();
    }
}
