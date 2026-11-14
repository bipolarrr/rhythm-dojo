using System;
using RhythmDojo.Application;
using RhythmDojo.Gameplay;
using RhythmDojo.UI;
using UnityEngine;

namespace RhythmDojo.Presentation
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class GameplayHitFeedback : MonoBehaviour
    {
        private const int PoolSize = 24;
        private static readonly Color Perfect = new Color(.68f, .83f, .72f);
        private static readonly Color Good = new Color(.96f, .76f, .46f);
        private static readonly Color Miss = new Color(.90f, .43f, .34f);
        private IGameplayReadModel model;
        private GameModeDefinition mode;
        private Camera targetCamera;
        private Canvas canvas;
        [SerializeField]
        private GameplayAtmosphere atmosphere;
        [SerializeField]
        private GameplayImpactGraphic[] bursts;
        [SerializeField]
        private GameplayImpactGraphic[] holdGlows;
        [SerializeField]
        private GameplayImpactGraphic holdTemplate;
        private int[] holdNotes;
        private int nextBurst;
        private int lastSoundFrame = -1;
        private AudioSource source;
        [SerializeField]
        private AudioClip perfectSound;
        [SerializeField]
        private AudioClip goodSound;
        [SerializeField]
        private AudioClip inputSound;
        [SerializeField]
        private AudioSource inputSource;
        private ILaneInput input;
        private RhythmGameController controller;
        private int lastInputFrame = -1;

        public void BindInput(ILaneInput input, RhythmGameController controller)
        {
            if (this.input != null)
            {
                this.input.Pressed -= OnInput;
            }
            this.input = input;
            this.controller = controller;
            input.Pressed += OnInput;
        }

        private void OnInput(int lane, double timestamp)
        {
            if (!controller || controller.IsPaused)
            {
                return;
            }
            if (controller.Session.State != SessionState.Playing || !controller.Clock.Running)
            {
                return;
            }
            if (lastInputFrame == Time.frameCount)
            {
                return;
            }
            lastInputFrame = Time.frameCount;
            // One attack per chord, short preloaded clips; no per-input allocations.
            inputSource.PlayOneShot(inputSound, .24f);
        }
        [SerializeField]
        private RectTransform effectsRoot;

        public void Initialize(
            IGameplayReadModel readModel,
            GameModeDefinition gameMode,
            Camera camera,
            Canvas hudCanvas
        )
        {
            Unbind();

            if ((readModel == null || !gameMode) || !camera)
            {
                throw new InvalidOperationException(
                    "Hit feedback dependencies missing or lane counts differ."
                );
            }

            if (!hudCanvas || readModel.LaneCount != gameMode.LaneCount)
            {
                throw new InvalidOperationException(
                    "Hit feedback dependencies missing or lane counts differ."
                );
            }

            model = readModel;
            mode = gameMode;
            targetCamera = camera;
            canvas = hudCanvas;
            source = GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0;
            source.volume = .25f;
            source.priority = 96;

            if (!perfectSound || !goodSound)
            {
                throw new InvalidOperationException("Dojo hit sounds missing.");
            }
            if (!inputSound || !inputSource)
            {
                throw new InvalidOperationException("Authored strike input audio missing.");
            }

            if (!effectsRoot || !holdTemplate)
            {
                throw new InvalidOperationException("Authored hit feedback layout missing.");
            }

            if (!atmosphere)
            {
                throw new InvalidOperationException("Authored gameplay atmosphere missing.");
            }
            atmosphere.Validate();

            if (bursts == null || bursts.Length != PoolSize)
            {
                throw new InvalidOperationException("Authored hit burst pool missing.");
            }

            if (Array.Exists(bursts, burst => !burst))
            {
                throw new InvalidOperationException("Authored hit burst reference missing.");
            }

            // Only the mode-dependent hold count is dynamic; fixed burst objects are reused.
            if (holdGlows == null || holdGlows.Length != mode.LaneCount)
            {
                if (holdGlows != null)
                {
                    foreach (var graphic in holdGlows)
                    {
                        if (graphic)
                        {
                            graphic.gameObject.SetActive(false);
                            Destroy(graphic.gameObject);
                        }
                    }
                }

                holdGlows = new GameplayImpactGraphic[mode.LaneCount];
                for (int i = 0; i < mode.LaneCount; i++)
                {
                    holdGlows[i] = Instantiate(holdTemplate, effectsRoot);
                    holdGlows[i].name = "Hold Glow " + i;
                    holdGlows[i].Lane = i;
                }
            }

            holdNotes = new int[mode.LaneCount];

            model.Judged += OnJudged;
            model.HoldStarted += OnHoldStarted;
            model.ResetOccurred += ResetFeedback;
            ResetFeedback();
        }

        private void OnHoldStarted(HoldStartedEvent result)
        {
            holdNotes[result.Head.Lane] = result.Head.NoteIndex;
            holdGlows[result.Head.Lane]
                .Sustain(
                    (
                        result.Head.Grade switch
                        {
                            Judgment.Perfect => Perfect,
                            _ => Good,
                        }
                    )
                );
            Impact(result.Head, 1);
        }

        private void OnJudged(JudgmentEvent result)
        {
            if (result.Kind == NoteKind.Hold)
            {
                holdNotes[result.Lane] = -1;
                holdGlows[result.Lane].gameObject.SetActive(false);
            }

            Impact(
                result,
                (
                    result.Kind switch
                    {
                        NoteKind.Hold => .7f,
                        _ => 1,
                    }
                )
            );
        }

        private void Impact(JudgmentEvent result, float strength)
        {
            // Retire the lane's previous burst during dense repeats; chords remain independent.
            foreach (var burst in bursts)
            {
                if (burst.gameObject.activeSelf && burst.Lane == result.Lane)
                {
                    burst.gameObject.SetActive(false);
                }
            }
            var graphic = bursts[nextBurst];
            nextBurst = (nextBurst + 1) % PoolSize;
            graphic.Lane = result.Lane;
            graphic.Variation = nextBurst % 3;
            var tint = Miss;
            switch (result.Grade)
            {
                case Judgment.Perfect:
                    tint = Perfect;
                    break;
                case Judgment.Good:
                    tint = Good;
                    break;
                case Judgment.Miss:
                    strength = .55f;
                    break;
            }
            graphic.Play(tint, strength, result.Grade);
            Position(graphic);
            atmosphere.React(result.Grade);

            if (result.Grade == Judgment.Miss)
            {
                return;
            }

            // A chord gets one attack sound so six simultaneous lanes cannot multiply the volume.
            if (lastSoundFrame == Time.frameCount)
            {
                return;
            }

            lastSoundFrame = Time.frameCount;
            source.PlayOneShot(
                (
                    result.Grade switch
                    {
                        Judgment.Perfect => perfectSound,
                        _ => goodSound,
                    }
                ),
                strength
            );
        }

        private void LateUpdate()
        {
            if (model == null)
            {
                return;
            }

            foreach (var graphic in bursts)
            {
                if (graphic.gameObject.activeSelf)
                {
                    Position(graphic);
                }
            }

            for (int i = 0; i < holdGlows.Length; i++)
            {
                bool holding =
                    holdNotes[i] >= 0 && model.GetNoteState(holdNotes[i]) == NoteState.Holding;

                if (!holding)
                {
                    if (holdGlows[i])
                    {
                        holdGlows[i].gameObject.SetActive(false);
                    }

                    holdNotes[i] = -1;
                }
                else
                {
                    Position(holdGlows[i]);
                }
            }
        }

        private void Position(GameplayImpactGraphic graphic)
        {
            var rect = (RectTransform)canvas.transform;
            var world = new Vector3(mode.LaneX(graphic.Lane), .12f, 0);
            var screen = targetCamera.WorldToScreenPoint(world);
            Camera uiCamera;

            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                uiCamera = null;
            }
            else
            {
                uiCamera = canvas.worldCamera;
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rect,
                screen,
                uiCamera,
                out var point
            );
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rect,
                targetCamera.WorldToScreenPoint(world + Vector3.right * mode.LaneSpacing),
                uiCamera,
                out var edge
            );
            graphic.rectTransform.anchoredPosition = point;
            graphic.rectTransform.sizeDelta = new Vector2(Mathf.Abs(edge.x - point.x) * .9f, 140);
        }

        private void ResetFeedback()
        {
            nextBurst = 0;
            lastSoundFrame = -1;
            lastInputFrame = -1;
            if (inputSource)
            {
                inputSource.Stop();
            }
            if (atmosphere)
            {
                atmosphere.ResetPresentation();
            }

            if (source)
            {
                source.Stop();
            }

            if (bursts != null)
            {
                foreach (var graphic in bursts)
                {
                    if (graphic)
                    {
                        graphic.gameObject.SetActive(false);
                    }
                }
            }

            if (holdGlows != null)
            {
                for (int i = 0; i < holdGlows.Length; i++)
                {
                    if (holdGlows[i])
                    {
                        holdGlows[i].gameObject.SetActive(false);
                    }

                    holdNotes[i] = -1;
                }
            }
        }

        public void Unbind()
        {
            if (input != null)
            {
                input.Pressed -= OnInput;
                input = null;
            }
            if (model != null)
            {
                model.Judged -= OnJudged;
                model.HoldStarted -= OnHoldStarted;
                model.ResetOccurred -= ResetFeedback;
            }

            ResetFeedback();
            model = null;
        }

        private void OnDisable() => Unbind();

        private void OnDestroy() => Unbind();
    }
}
