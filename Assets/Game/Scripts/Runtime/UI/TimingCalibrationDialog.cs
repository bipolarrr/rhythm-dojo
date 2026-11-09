using System;
using System.Collections.Generic;
using RhythmDojo.Application;
using RhythmDojo.Audio;
using RhythmDojo.Gameplay;
using RhythmDojo.Input;
using RhythmDojo.Services;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace RhythmDojo.UI
{
    internal sealed class TimingCalibrationDialog : IDisposable
    {
        private const int WarmupBeats = 4;
        private const int MeasuredBeats = 12;
        internal const int TestBpm = 120;
        private const double BeatInterval = 60.0 / TestBpm;
        private const double TapWindow = .2;

        private readonly Action<int> applyOffset;
        private readonly PresentationPreferences presentation;
        private readonly ISelectionPreferences preferences;
        private readonly Func<IScrollTimeline> createSpeedTimeline;
        private readonly Func<JudgmentSettings> getJudgmentTiming;

        private readonly TimingCalibrationView calibration;
        private readonly GameObject overlay;
        private readonly Button entry;
        private readonly Button speedEntry;
        private readonly Button start;
        private readonly Button apply;
        private readonly Button close;
        private readonly Text status;
        private readonly Text heading;
        private readonly RectTransform panelRect;
        private readonly SpeedTestLane speedLane;
        private readonly Slider speedSlider;

        private readonly AudioSource source;
        private readonly InputAction tap =
            new InputAction("Calibration tap", InputActionType.Button, "<Keyboard>/space");
        private readonly HashSet<int> recordedBeats = new HashSet<int>();
        private readonly List<double> errors = new List<double>();
        private TitleMusicPlayer titleMusic;
        private AudioClip clip;

        private double firstDspBeat;
        private double firstInputBeat;
        private double finishTime;
        private int? recommendation;
        private bool measuring;
        private bool audioChanged;

        private IScrollTimeline speedTimeline;
        private JudgmentSettings speedTiming;
        private double speedOffset;
        private bool speedTest;
        private bool speedPaused;
        private double pausedSpeedTime;
        private double speedResumeInputTime;

        public Button Entry => entry;
        public Button SpeedEntry => speedEntry;

        public TimingCalibrationDialog(
            SettingsView view,
            PresentationPreferences presentation,
            ISelectionPreferences preferences,
            Action<int> applyOffset,
            Func<IScrollTimeline> createSpeedTimeline,
            Func<JudgmentSettings> getJudgmentTiming
        )
        {
            this.applyOffset = applyOffset;
            this.presentation = presentation;
            this.preferences = preferences;
            this.createSpeedTimeline = createSpeedTimeline;
            this.getJudgmentTiming = getJudgmentTiming;
            speedSlider = view.speedSlider;

            var calibration = view.calibration;
            calibration.Validate();
            this.calibration = calibration;
            entry = calibration.entry;
            speedEntry = calibration.speedEntry;
            overlay = calibration.overlay;
            panelRect = calibration.panelRect;
            heading = calibration.heading;
            status = calibration.status;
            start = calibration.start;
            apply = calibration.apply;
            close = calibration.close;
            speedLane = new SpeedTestLane(calibration.speedLane);
            source = calibration.source;
            entry.onClick.AddListener(OpenTiming);
            speedEntry.onClick.AddListener(OpenSpeed);
            start.onClick.AddListener(StartTest);
            apply.onClick.AddListener(Apply);
            close.onClick.AddListener(Close);
            tap.performed += RecordTap;
            overlay.SetActive(false);
        }

        private void OpenTiming() => Open(false);

        private void OpenSpeed() => Open(true);

        internal void Open(bool useSpeedTest)
        {
            speedTest = useSpeedTest;
            overlay.SetActive(true);
            recommendation = null;
            apply.gameObject.SetActive(!speedTest);
            apply.interactable = false;
            SetNavigation(false);
            start.GetComponentInChildren<Text>().text = "테스트 시작";
            speedLane.GameObject.SetActive(speedTest);
            status.gameObject.SetActive(true);

            if (speedTest)
            {
                calibration.speedLayout.Apply(panelRect, heading, status, start, close);
                heading.text = $"{speedSlider.value:0.0#}x";
                status.color = new Color(1f, .94f, .80f);
                status.text = "Space · 노트 입력\nP · 일시정지";
                speedLane.Reset();
            }
            else
            {
                calibration.timingLayout.Apply(panelRect, heading, status, start, close);
                heading.text = "입력 타이밍 테스트";
                status.color = heading.color;
                status.text = "소리를 듣고 박자마다 Space를 누르세요.\n처음 네 박은 준비, 이후 열두 박을 측정합니다.";
            }

            EventSystem.current?.SetSelectedGameObject(start.gameObject);
        }

        private void StartTest()
        {
            if (measuring)
            {
                if (speedTest)
                {
                    ToggleSpeedPause();
                }

                return;
            }

            if (AudioListener.pause)
            {
                status.text = "오디오 일시정지를 해제한 뒤 다시 시도해 주세요.";
                return;
            }

            if (!speedTest && (AudioListener.volume <= 0 || presentation.gameplayVolume <= 0))
            {
                status.text = "박자 소리를 들을 수 있게 음량을 올린 뒤 다시 시도해 주세요.";
                return;
            }

            int sampleRate = AudioSettings.outputSampleRate;
            if (sampleRate <= 0)
            {
                status.text = "오디오 출력 장치를 확인한 뒤 다시 시도해 주세요.";
                return;
            }

            if (!clip || clip.frequency != sampleRate)
            {
                DestroyClip();
                clip = CreateBeatTrack(sampleRate);
            }

            titleMusic = Object.FindFirstObjectByType<TitleMusicPlayer>();
            titleMusic?.SetCalibrationMuted(true);
            recommendation = null;
            errors.Clear();
            recordedBeats.Clear();
            start.interactable = speedTest;
            apply.interactable = false;
            SetNavigation(false);
            EventSystem.current?.SetSelectedGameObject(null);

            source.clip = clip;
            source.volume = .65f * presentation.gameplayVolume;
            source.loop = speedTest;

            double lead = .25;
            if (speedTest)
            {
                lead = 1.75;
                speedTimeline = createSpeedTimeline();
                speedTiming = getJudgmentTiming();
                speedOffset = preferences.TimingOffsetMs / 1000.0;
                pausedSpeedTime = double.NegativeInfinity;
                speedResumeInputTime = double.NegativeInfinity;
                speedPaused = false;
                speedLane.Reset();
            }

            double dspTime = AudioSettings.dspTime;
            firstDspBeat = dspTime + lead;
            firstInputBeat = Time.realtimeSinceStartupAsDouble + (firstDspBeat - dspTime);
            finishTime = firstInputBeat + (WarmupBeats + MeasuredBeats - 1) * BeatInterval + TapWindow + .05;

            audioChanged = false;
            AudioSettings.OnAudioConfigurationChanged += AudioConfigurationChanged;
            measuring = true;
            source.PlayScheduled(firstDspBeat);
            tap.Enable();

            if (speedTest)
            {
                start.GetComponentInChildren<Text>().text = "일시정지";
                status.gameObject.SetActive(false);
            }
            else
            {
                status.text = "준비 4박 · 소리를 들으세요";
            }
        }

        private void ToggleSpeedPause()
        {
            double dspTime = AudioSettings.dspTime;
            if (!speedPaused)
            {
                pausedSpeedTime = Math.Max(pausedSpeedTime, dspTime - firstDspBeat);
                speedPaused = true;
                tap.Disable();
                source.Stop();
                speedLane.Render(pausedSpeedTime, BeatInterval, speedTimeline);
                status.text = "일시정지";
                status.gameObject.SetActive(true);
                start.GetComponentInChildren<Text>().text = "계속하기";

                return;
            }

            // Restart the click loop at the frozen sample and use the same lead for the input clock.
            const double resumeLead = .15;
            double resumeDspTime = dspTime + resumeLead;
            speedResumeInputTime = Time.realtimeSinceStartupAsDouble + resumeLead;
            firstDspBeat = resumeDspTime - pausedSpeedTime;
            firstInputBeat = speedResumeInputTime - pausedSpeedTime;

            if (pausedSpeedTime >= 0)
            {
                source.timeSamples = (int)Math.Round(pausedSpeedTime * clip.frequency % clip.samples) % clip.samples;
                source.PlayScheduled(resumeDspTime);
            }
            else
            {
                source.timeSamples = 0;
                source.PlayScheduled(firstDspBeat);
            }

            speedPaused = false;
            status.gameObject.SetActive(false);
            start.GetComponentInChildren<Text>().text = "일시정지";
            EventSystem.current?.SetSelectedGameObject(null);
            tap.Enable();
        }

        private static AudioClip CreateBeatTrack(int sampleRate)
        {
            int beatSamples = (int)(sampleRate * BeatInterval);
            var samples = new float[beatSamples * (WarmupBeats + MeasuredBeats)];
            int clickSamples = sampleRate / 25;

            for (int beat = 0; beat < WarmupBeats + MeasuredBeats; beat++)
            {
                for (int i = 0; i < clickSamples; i++)
                {
                    float envelope = 1f - (float)i / clickSamples;
                    samples[beat * beatSamples + i] =
                        Mathf.Sin(i * (2 * Mathf.PI * 1000 / sampleRate)) * envelope * envelope;
                }
            }

            var track = AudioClip.Create("Calibration beats", samples.Length, 1, sampleRate, false);
            track.SetData(samples, 0);

            return track;
        }

        private void RecordTap(InputAction.CallbackContext context)
        {
            double elapsed = context.time - firstInputBeat;
            if (speedTest)
            {
                if (speedPaused || context.time < speedResumeInputTime)
                {
                    return;
                }

                double time = InputTiming.Calibrate(elapsed, speedOffset);
                if (speedLane.TryHit(time, BeatInterval, speedTiming, out var grade, out double timingError))
                {
                    speedLane.ShowFeedback(grade, $"{timingError * 1000:+0;-0;0} ms", elapsed);
                }

                return;
            }

            int beat = (int)Math.Round(elapsed / BeatInterval);
            if (beat < WarmupBeats || beat >= WarmupBeats + MeasuredBeats)
            {
                return;
            }

            double error = elapsed - beat * BeatInterval;
            if (Math.Abs(error) <= TapWindow && recordedBeats.Add(beat))
            {
                errors.Add(error);
            }
        }

        public bool Tick()
        {
            if (!overlay.activeSelf)
            {
                return false;
            }

            if (Keyboard.current?.escapeKey.wasPressedThisFrame == true)
            {
                Close();
                return true;
            }

            if (!measuring)
            {
                return true;
            }

            if (audioChanged || !ApplicationFocusState.IsFocused)
            {
                StopTest();
                status.text = "창 또는 오디오 환경이 바뀌었습니다. 테스트를 다시 시작해 주세요.";
                status.gameObject.SetActive(true);
            }
            else if (speedTest)
            {
                TickSpeedTest();
            }
            else if (Time.realtimeSinceStartupAsDouble >= finishTime)
            {
                ShowResult();
            }
            else if (AudioSettings.dspTime >= firstDspBeat + WarmupBeats * BeatInterval)
            {
                status.text = $"측정 중 · {errors.Count}/{MeasuredBeats}회";
            }

            return true;
        }

        private void TickSpeedTest()
        {
            if (Keyboard.current?.pKey.wasPressedThisFrame == true)
            {
                ToggleSpeedPause();
            }

            if (speedPaused)
            {
                return;
            }

            double time = Math.Max(pausedSpeedTime, AudioSettings.dspTime - firstDspBeat);
            double sweepTime = time - Math.Max(0, speedOffset);
            if (speedLane.Advance(sweepTime, BeatInterval, speedTiming))
            {
                speedLane.ShowFeedback(Judgment.Miss, "미입력", time);
            }

            speedLane.Render(time, BeatInterval, speedTimeline);
        }

        private void ShowResult()
        {
            StopTest();
            recommendation = InputTiming.RecommendOffsetMs(errors, 0, 8);
            apply.interactable = recommendation.HasValue;
            SetNavigation(recommendation.HasValue);

            if (recommendation is not { } value)
            {
                status.text = "입력이 부족하거나 일정하지 않습니다.\n소리에 맞춰 한 번 더 테스트해 주세요.";
                return;
            }

            status.text = $"테스트 결과 · {errors.Count}/{MeasuredBeats}회\n"
                + $"현재 {preferences.TimingOffsetMs:+0;-0;0} ms → 추천 {value:+0;-0;0} ms";
            EventSystem.current?.SetSelectedGameObject(apply.gameObject);
        }

        private void SetNavigation(bool hasResult)
        {
            Selectable middle = null;
            if (hasResult)
            {
                middle = apply;
            }

            start.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnRight = middle ?? close,
            };
            apply.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnLeft = start,
                selectOnRight = close,
            };
            close.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnLeft = middle ?? start,
            };
        }

        private void Apply()
        {
            if (recommendation is not { } value)
            {
                return;
            }

            applyOffset(value);
            Close();
        }

        private void Close()
        {
            StopTest();
            overlay.SetActive(false);

            var previousEntry = entry;
            if (speedTest)
            {
                previousEntry = speedEntry;
            }

            EventSystem.current?.SetSelectedGameObject(previousEntry.gameObject);
        }

        private void StopTest()
        {
            if (!measuring)
            {
                return;
            }

            measuring = false;
            tap.Disable();
            source.Stop();
            speedLane.Reset();
            AudioSettings.OnAudioConfigurationChanged -= AudioConfigurationChanged;

            if (titleMusic)
            {
                titleMusic.SetCalibrationMuted(false);
            }

            titleMusic = null;
            start.interactable = true;
            start.GetComponentInChildren<Text>().text = "다시 테스트";
            EventSystem.current?.SetSelectedGameObject(start.gameObject);
        }

        private void AudioConfigurationChanged(bool deviceWasChanged) => audioChanged = true;

        private void DestroyClip()
        {
            if (clip)
            {
                source.clip = null;
                Object.Destroy(clip);
            }
        }

        public void Dispose()
        {
            StopTest();
            tap.Dispose();
            DestroyClip();
            overlay.SetActive(false);
            entry.onClick.RemoveListener(OpenTiming);
            speedEntry.onClick.RemoveListener(OpenSpeed);
            start.onClick.RemoveListener(StartTest);
            apply.onClick.RemoveListener(Apply);
            close.onClick.RemoveListener(Close);
        }
    }
}
