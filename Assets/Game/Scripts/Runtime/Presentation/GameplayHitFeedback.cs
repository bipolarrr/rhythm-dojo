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
        private static readonly Color Perfect = new Color(.46f, .91f, .85f);
        private static readonly Color Good = new Color(1, .77f, .42f);
        private static readonly Color Miss = new Color(.96f, .29f, .31f);
        private IGameplayReadModel model;
        private GameModeDefinition mode;
        private Camera targetCamera;
        private Canvas canvas;
        private Vector3 cameraOrigin;
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
        private double kickAt = -10;
        private float kickStrength;
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
            cameraOrigin = camera.transform.position;
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
            var graphic = bursts[nextBurst];
            nextBurst = (nextBurst + 1) % PoolSize;
            graphic.Lane = result.Lane;
            graphic.Play(
                (
                    result.Grade switch
                    {
                        Judgment.Perfect => Perfect,
                        _ => (
                            result.Grade switch
                            {
                                Judgment.Good => Good,
                                _ => Miss,
                            }
                        ),
                    }
                ),
                (
                    result.Grade switch
                    {
                        Judgment.Miss => .35f,
                        _ => strength,
                    }
                ),
                result.Grade == Judgment.Miss
            );
            Position(graphic);

            if (result.Grade == Judgment.Miss)
            {
                return;
            }

            kickAt = Time.unscaledTimeAsDouble;
            kickStrength = Mathf.Min(.025f, .025f * strength);

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

            float age = (float)(Time.unscaledTimeAsDouble - kickAt);
            float envelope = Mathf.Clamp01(1 - age / .10f);
            targetCamera.transform.position =
                cameraOrigin
                + Vector3.up
                    * (kickStrength * envelope * envelope * Mathf.Sin(age / .10f * Mathf.PI * 2));

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
            kickAt = -10;
            kickStrength = 0;

            if (targetCamera)
            {
                targetCamera.transform.position = cameraOrigin;
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
