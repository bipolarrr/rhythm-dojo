using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using RhythmDojo.Application;
using RhythmDojo.Content;
using RhythmDojo.Gameplay;
using RhythmDojo.UI;
using UnityEngine;
using UnityEngine.UI;

namespace RhythmDojo.Authoring
{
    // Additive editor extension. The existing menu remains responsible for saving and playtests.
    public sealed class ChartAudioTimeline : MonoBehaviour
    {
        [SerializeField] private ChartAudioTimelineView view;
        private bool bound;
        private ChartEditorScreen screen;
        private ChartBeatGrid grid;
        private EditorSession observed;
        private SongProject snapshot;
        private FileSongProjectStore store;
        [SerializeField] private AudioSource source;
        private Func<string, CancellationToken, Task<SongAudioLease>> loadAudio;
        private SongAudioLease lease;
        private CancellationTokenSource loading;
        private Coroutine building;
        private bool loadingAudio;
        private string audioKey;
        private double pausedTime;
        private double playbackStart;
        private double playbackDsp;
        private bool playing;
        private bool follow = true;
        private bool locked;
        private bool preserveBeats;
        private float panelHeight = 280;
        private RectTransform panel;
        private RectTransform content;
        private GameObject advanced;
        private AudioTimelineGraphic overview;
        private AudioTimelineGraphic detail;
        private Text clock;
        private Text message;
        private Text tooltip;
        private Text ruler;
        private Text playLabel;
        private InputField bpm;
        private InputField origin;
        private InputField nudge;
        private InputField segmentTime;
        private InputField segmentBpm;
        private InputField numerator;
        private InputField denominator;
        private InputField firstBeat;
        private InputField secondBeat;
        private InputField beatCount;
        private Dropdown segments;
        private Dropdown nudgeUnit;
        private int selected;
        private bool filling;
        private double lastScrollBeat;

        public WaveformPeaks Peaks { get; private set; }
        public BeatGridTiming Timing { get; private set; }
        public double WindowStart { get; private set; }
        public double WindowDuration { get; private set; } = 8;
        public double DisplayStep { get; private set; } = 0.25;
        public double AudioOffset => snapshot?.AudioOffsetSeconds ?? 0;
        public double Duration
        {
            get
            {
                if (!source || !source.clip)
                {
                    return 0;
                }

                return (double)source.clip.samples / source.clip.frequency;
            }
        }
        public bool IsBusy => !screen || screen.IsBusy || loadingAudio;

        public double MusicTime
        {
            get
            {
                if (playing)
                {
                    return Math.Min(Duration, playbackStart + Math.Max(0, AudioSettings.dspTime - playbackDsp));
                }

                return pausedTime;
            }
        }

        public void ConfigureAudio(Func<string, CancellationToken, Task<SongAudioLease>> loadAudio)
        {
            this.loadAudio = loadAudio ?? throw new ArgumentNullException(nameof(loadAudio));
        }

        public void Initialize(ChartEditorScreen screen, ChartBeatGrid grid, FileSongProjectStore store)
        {
            view.ValidateReferences();
            this.screen = screen;
            this.grid = grid;
            this.store = store;

            panel = view.panel;
            content = view.content;
            advanced = view.advanced;
            overview = view.overview;
            detail = view.detail;
            clock = view.clock;
            message = view.message;
            tooltip = view.tooltip;
            ruler = view.ruler;
            playLabel = view.playLabel;
            bpm = view.bpm;
            origin = view.origin;
            nudge = view.nudge;
            segmentTime = view.segmentTime;
            segmentBpm = view.segmentBpm;
            numerator = view.numerator;
            denominator = view.denominator;
            firstBeat = view.firstBeat;
            secondBeat = view.secondBeat;
            beatCount = view.beatCount;
            segments = view.segments;
            nudgeUnit = view.nudgeUnit;
            BindControls();
            audioKey = null;
            Observe();
        }

        private void BindControls()
        {
            if (bound)
            {
                return;
            }

            bound = true;
            view.playAudioControl.onClick.AddListener(() =>
                {
                    if (playing)
                    {
                        Pause();
                    }
                    else
                    {
                        Play();
                    }
                });
            view.followAudioControl.onClick.AddListener(() =>
                {
                    follow = true;
                    SyncChart(MusicTime);
                });
            view.applyGridControl.onClick.AddListener(ApplyBasic);
            view.cursorOriginControl.onClick.AddListener(SetOrigin);
            view.nudgeUnitsControl.onValueChanged.AddListener(_ => { });
            view.nudgeEarlierControl.onClick.AddListener(() => Shift(-1));
            view.nudgeLaterControl.onClick.AddListener(() => Shift(1));
            view.doubleBPMControl.onClick.AddListener(() => ScaleBpm(2));
            view.halfBPMControl.onClick.AddListener(() => ScaleBpm(0.5));
            view.gridLockControl.onClick.AddListener(() =>
            {
                if (IsBusy)
                {
                    return;
                }

                locked = !locked;
                view.lockLabel.text = "잠금 OFF";
                if (locked)
                {
                    view.lockLabel.text = "잠금 ON";
                }
            });
            view.noteSnapControl.onValueChanged.AddListener(value =>
                {
                    if (IsBusy || locked)
                    {
                        return;
                    }

                    screen.SnapEnabled = value != 6;
                    if (value < 6)
                    {
                        screen.SnapBeats = new[] { 1.0, 0.5, 0.25, 0.125, 1.0 / 3, 1.0 / 6 }[value];
                    }
                });
            view.closeAdvancedControl.onClick.AddListener(() => advanced.SetActive(false));
            view.tempoSegmentsControl.onValueChanged.AddListener(value =>
            {
                if (!filling)
                {
                    selected = value;
                    FillFields();
                }
            });
            view.addTempoSegmentControl.onClick.AddListener(() => ChangeSegment(true));
            view.applyTempoSegmentControl.onClick.AddListener(() => ChangeSegment(false));
            view.deleteTempoSegmentControl.onClick.AddListener(DeleteSegment);
            view.alignTempoSegmentControl.onClick.AddListener(AlignSegment);
            view.captureFirstBeatControl.onClick.AddListener(() => Edit(() => firstBeat.text = MusicTime.ToString("R", CultureInfo.InvariantCulture), false));
            view.captureSecondBeatControl.onClick.AddListener(() => Edit(() => secondBeat.text = MusicTime.ToString("R", CultureInfo.InvariantCulture), false));
            view.calculateBPMControl.onClick.AddListener(CalculateBpm);
            view.existingNotePolicyControl.onValueChanged.AddListener(value => Edit(() => preserveBeats = value == 1, false));
            view.displayDensityControl.onValueChanged.AddListener(value => Edit(() =>
                {
                    DisplayStep = new[] { 1.0, 0.5, 0.25, 0.125, 1.0 / 3, 1.0 / 6 }[value];
                    Redraw();
                }, false));
            view.advancedGridControl.onClick.AddListener(() => menu.ToggleAdvanced());
        }

        [SerializeField] private ChartEditorMenu menu;

        public void UseStore(FileSongProjectStore store)
        {
            this.store = store;
            audioKey = null;
            Observe();
        }

        private void Update()
        {
            if (!screen || !panel)
            {
                return;
            }

            if (observed != screen.Session)
            {
                Observe();
            }

            if (screen.IsBusy && playing)
            {
                Pause();
            }

            foreach (var control in view.editControls)
            {
                control.interactable = !IsBusy && !locked;
            }

            double time = MusicTime;
            if (playing && time >= Duration)
            {
                Pause();
            }

            if (playing && follow)
            {
                WindowStart = Math.Max(0, time - WindowDuration * 0.3);
                SyncChart(time);
            }

            if (playing)
            {
                Redraw();
            }

            if (Math.Abs(lastScrollBeat - screen.ScrollBeatOffset) > 1e-8)
            {
                WindowStart = Math.Max(0, Timing.TimeAtBeat(screen.ScrollBeatOffset) - AudioOffset);
                lastScrollBeat = screen.ScrollBeatOffset;
                Redraw();
            }

            if (grid.PlayheadTime != time + AudioOffset)
            {
                grid.PlayheadTime = time + AudioOffset;
                grid.SetVerticesDirty();
            }

            clock.text = FormatTime(time) + " / " + FormatTime(Duration);
            if (Timing != null)
            {
                double beat = Timing.BeatAtTime(time + AudioOffset);
                Timing.GetMusicalPosition(beat, out int measure, out double position);
                ruler.text = "마디 " + measure + " · 박 " + (position + 1).ToString("0.00")
                    + "   |   " + WindowStart.ToString("0.00") + "–"
                    + (WindowStart + WindowDuration).ToString("0.00") + " s";
            }

            if (Peaks != null && !Peaks.Ready && !loadingAudio)
            {
                message.text = "파형 생성 중 " + (Peaks.Progress * 100).ToString("0") + "%";
            }
        }

        private static string FormatTime(double time)
        {
            return TimeSpan.FromSeconds(Math.Max(0, time)).ToString(@"mm\:ss\.fff");
        }

        private void Observe()
        {
            if (observed != null)
            {
                observed.Changed -= Refresh;
            }

            observed = screen.Session;
            observed.Changed += Refresh;
            Refresh();
        }

        private void Refresh()
        {
            snapshot = observed.Snapshot;
            Timing = new BeatGridTiming(snapshot.TempoPoints, snapshot.Grid);
            selected = Math.Min(selected, snapshot.TempoPoints.Length - 1);
            FillFields();
            Redraw();
            string key = snapshot.Id.ToString("D") + "/" + snapshot.Audio;
            if (key != audioKey)
            {
                audioKey = key;
                LoadAudio(snapshot);
            }
        }

        private async void LoadAudio(SongProject project)
        {
            loading?.Cancel();
            loading?.Dispose();
            loading = new CancellationTokenSource();
            var token = loading.Token;
            Pause();
            if (building != null)
            {
                StopCoroutine(building);
                building = null;
            }

            source.clip = null;
            lease?.Dispose();
            lease = null;
            Peaks = null;
            pausedTime = 0;
            WindowStart = 0;
            Redraw();
            if (project.Audio == null)
            {
                loadingAudio = false;
                message.text = "파일 → 음원 가져오기 · 파형 클릭: 탐색 · 드래그: 이동 · Ctrl+휠: 확대";
                return;
            }

            loadingAudio = true;
            message.text = "음원 로딩 중…";
            SongAudioLease loaded = null;
            try
            {
                loaded = await loadAudio(
                    store.ResolveAudioPath(project.Id, project.Audio), token);
                token.ThrowIfCancellationRequested();
                lease = loaded;
                loaded = null;
                source.clip = lease.Clip;
                Peaks = new WaveformPeaks();
                building = StartCoroutine(BuildPeaks(Peaks, lease.Clip));
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                if (this && !token.IsCancellationRequested)
                {
                    message.text = e.Message;
                }
            }
            finally
            {
                loaded?.Dispose();
                if (this && !token.IsCancellationRequested)
                {
                    loadingAudio = false;
                }
            }
        }

        private IEnumerator BuildPeaks(WaveformPeaks peaks, AudioClip clip)
        {
            var routine = peaks.Build(clip);
            while (true)
            {
                bool next;
                try
                {
                    next = routine.MoveNext();
                }
                catch (Exception e)
                {
                    message.text = e.Message;
                    Peaks = null;
                    yield break;
                }

                if (!next)
                {
                    break;
                }

                yield return routine.Current;
            }

            message.text = "파형 준비 완료 · 빨강: 마디 · 흰색: 재생 위치";
            building = null;
            Redraw();
        }

        public void Seek(double time)
        {
            if (IsBusy || !source.clip)
            {
                return;
            }

            bool resume = playing;
            Pause();
            pausedTime = Math.Max(0, Math.Min(Duration, time));
            source.timeSamples = Math.Min(source.clip.samples - 1,
                (int)(pausedTime * source.clip.frequency));
            WindowStart = Math.Max(0, pausedTime - WindowDuration * 0.3);
            SyncChart(pausedTime);
            if (resume && pausedTime < Duration)
            {
                Play();
            }

            Redraw();
        }

        private void Play()
        {
            if (IsBusy || !source.clip)
            {
                return;
            }

            if (pausedTime >= Duration)
            {
                pausedTime = 0;
            }

            source.Stop();
            source.timeSamples = Math.Min(source.clip.samples - 1, (int)(pausedTime * source.clip.frequency));
            playbackStart = (double)source.timeSamples / source.clip.frequency;
            playbackDsp = AudioSettings.dspTime + 0.05;
            source.PlayScheduled(playbackDsp);
            playing = true;
            playLabel.text = "일시정지 / Pause";
        }

        private void Pause()
        {
            if (playing)
            {
                pausedTime = MusicTime;
            }

            playing = false;
            source?.Stop();
            if (playLabel)
            {
                playLabel.text = "재생 / Play";
            }
        }

        public void Pan(double start)
        {
            if (IsBusy)
            {
                return;
            }

            follow = false;
            WindowStart = Math.Max(0, Math.Min(Math.Max(0, Duration - WindowDuration), start));
            SyncChart(WindowStart);
            Redraw();
        }

        public void Zoom(double steps, double anchorTime)
        {
            if (IsBusy || steps == 0)
            {
                return;
            }

            follow = false;
            double fraction = (anchorTime - WindowStart) / WindowDuration;
            double minimumDuration = 0.001;
            if (source.clip)
            {
                minimumDuration = 16.0 / source.clip.frequency;
            }

            WindowDuration = Math.Max(minimumDuration, Math.Min(Math.Max(8, Duration),
                WindowDuration * Math.Pow(0.8, Math.Sign(steps))));
            WindowStart = Math.Max(0, anchorTime - fraction * WindowDuration);
            SyncChart(WindowStart);
            Redraw();
        }

        private void SyncChart(double musicTime)
        {
            if (Timing == null)
            {
                return;
            }

            double beat = Timing.BeatAtTime(musicTime + AudioOffset);
            screen.Scroll(beat - screen.ScrollBeatOffset);
            lastScrollBeat = screen.ScrollBeatOffset;
        }

        private void Redraw()
        {
            overview?.SetVerticesDirty();
            detail?.SetVerticesDirty();
            detail?.RefreshLabels();
        }

        private void FillFields()
        {
            filling = true;
            bpm.SetTextWithoutNotify(snapshot.TempoPoints[0].Bpm.ToString("R", CultureInfo.InvariantCulture));
            origin.SetTextWithoutNotify(((snapshot.Grid.OriginSeconds - AudioOffset) * 1000)
                .ToString("0.###", CultureInfo.InvariantCulture));
            segments.ClearOptions();
            segments.AddOptions(snapshot.TempoPoints.Select((point, i) =>
                i + ": " + (point.StartTimeSeconds - AudioOffset).ToString("0.000")
                    + " s · " + point.Bpm.ToString("0.###") + " BPM").ToList());
            segments.SetValueWithoutNotify(selected);
            var current = snapshot.TempoPoints[selected];
            segmentTime.SetTextWithoutNotify((current.StartTimeSeconds - AudioOffset)
                .ToString("R", CultureInfo.InvariantCulture));
            segmentBpm.SetTextWithoutNotify(current.Bpm.ToString("R", CultureInfo.InvariantCulture));
            int meterIndex = Timing.MeterIndexAt(current.StartTimeSeconds);
            var meter = snapshot.Grid.Meters[meterIndex];
            numerator.SetTextWithoutNotify(meter.Numerator.ToString());
            denominator.SetTextWithoutNotify(meter.Denominator.ToString());
            filling = false;
        }

        private static double Number(InputField field)
        {
            if (!double.TryParse(field.text, NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
            {
                throw new ArgumentException("소수점은 . 을 사용하여 숫자를 입력하세요.");
            }

            if (!double.IsFinite(number))
            {
                throw new ArgumentException("유한한 숫자를 입력하세요.");
            }

            return number;
        }

        private static int WholeNumber(InputField field)
        {
            double number = Number(field);
            if (number != Math.Truncate(number) || number < 1 || number > 32)
            {
                throw new ArgumentException("박자표에는 1–32 정수를 입력하세요.");
            }

            return (int)number;
        }

        private void Edit(Action action, bool notify = true)
        {
            if (IsBusy)
            {
                return;
            }

            if (locked)
            {
                message.text = "GRID 잠금을 해제하세요.";
                return;
            }

            try
            {
                action();
                if (!notify)
                {
                    return;
                }

                string policy = "노트 음악 시간 유지";
                if (preserveBeats)
                {
                    policy = "노트 박자 유지";
                }

                message.text = "GRID 적용 완료 · " + policy;
            }
            catch (Exception e)
            {
                message.text = e.Message;
            }
        }

        private void Apply(TempoPoint[] points, BeatGridSettings settings)
        {
            BeatGridEditing.Apply(observed, points, settings, preserveBeats);
        }

        private void ApplyOrigin(TempoPoint[] points, double time)
        {
            BeatGridEditing.ApplyOrigin(observed, points, snapshot.Grid, time, preserveBeats);
        }

        private void ApplyBasic()
        {
            Edit(() =>
            {
                var points = (TempoPoint[])snapshot.TempoPoints.Clone();
                points[0] = new TempoPoint(0, Number(bpm));
                ApplyOrigin(points, Number(origin) / 1000 + AudioOffset);
            });
        }

        private void Shift(int direction)
        {
            Edit(() =>
            {
                double amount = Number(nudge) * direction;
                double time;
                if (nudgeUnit.value == 0)
                {
                    time = snapshot.Grid.OriginSeconds + amount / 1000;
                }
                else
                {
                    time = Timing.TimeAtBeat(amount);
                }

                ApplyOrigin(snapshot.TempoPoints, time);
            });
        }

        private void ScaleBpm(double factor)
        {
            Edit(() => Apply(snapshot.TempoPoints.Select(p =>
                new TempoPoint(p.StartTimeSeconds, p.Bpm * factor)).ToArray(), snapshot.Grid));
        }

        private void SetOrigin()
        {
            Edit(() =>
            {
                var points = (TempoPoint[])snapshot.TempoPoints.Clone();
                points[0] = new TempoPoint(0, Number(bpm));
                ApplyOrigin(points, MusicTime + AudioOffset);
            });
        }

        private void AlignSegment()
        {
            if (selected == 0)
            {
                Edit(() => ApplyOrigin(snapshot.TempoPoints, MusicTime + AudioOffset));
                return;
            }

            ChangeSegment(false, true);
        }

        private void ChangeSegment(bool add, bool align = false)
        {
            Edit(() =>
            {
                double time = Number(segmentTime) + AudioOffset;
                if (add || align)
                {
                    time = MusicTime + AudioOffset;
                }

                if (!add && selected == 0)
                {
                    time = 0;
                }

                double value = Number(segmentBpm);
                var points = snapshot.TempoPoints.ToList();
                double previousTime = snapshot.TempoPoints[selected].StartTimeSeconds;
                if (!add)
                {
                    points.RemoveAt(selected);
                }

                if (time < 0 || points.Any(p => Math.Abs(p.StartTimeSeconds - time) < 1e-8))
                {
                    throw new ArgumentException("변경점은 0 이상이며 기존 위치와 중복될 수 없습니다.");
                }

                points.Add(new TempoPoint(time, value));
                var ordered = points.OrderBy(p => p.StartTimeSeconds).ToArray();
                var settings = snapshot.Grid.Copy();
                var meters = settings.Meters.ToList();
                if (!add && previousTime > 0)
                {
                    meters.RemoveAll(m => m.TimeSeconds == previousTime);
                }

                meters.RemoveAll(m => m.TimeSeconds == time);
                int beats = WholeNumber(numerator);
                int unit = WholeNumber(denominator);
                var active = snapshot.Grid.Meters[Timing.MeterIndexAt(time)];
                bool meterChanged = active.Numerator != beats || active.Denominator != unit;
                bool explicitMeter = snapshot.Grid.Meters.Any(m => m.TimeSeconds == previousTime);
                bool keepExistingMeter = !add && explicitMeter;
                bool anchorMeter = time == 0 || align;
                if (anchorMeter || meterChanged || keepExistingMeter)
                {
                    meters.Add(new MeterPoint(time, beats, unit));
                }

                settings.Meters = meters.OrderBy(m => m.TimeSeconds).ToArray();
                Apply(ordered, settings);
                selected = Array.FindIndex(ordered, p => p.StartTimeSeconds == time);
                FillFields();
            });
        }

        private void DeleteSegment()
        {
            Edit(() =>
            {
                if (selected == 0)
                {
                    throw new ArgumentException("첫 변경점은 삭제할 수 없습니다.");
                }

                double time = snapshot.TempoPoints[selected].StartTimeSeconds;
                var settings = snapshot.Grid.Copy();
                settings.Meters = settings.Meters.Where(m => m.TimeSeconds != time).ToArray();
                Apply(snapshot.TempoPoints.Where((p, i) => i != selected).ToArray(), settings);
            });
        }

        private void CalculateBpm()
        {
            Edit(() =>
            {
                double interval = Number(secondBeat) - Number(firstBeat);
                double count = Number(beatCount);
                if (interval <= 0 || count <= 0)
                {
                    throw new ArgumentException("B는 A보다 뒤에 있어야 하며 박 간격 수는 양수여야 합니다.");
                }

                segmentBpm.text = (count * 60 / interval).ToString("R", CultureInfo.InvariantCulture);
                message.text = "계산한 BPM을 구간 입력에 넣었습니다. 선택 구간 적용으로 저장하세요.";
            }, false);
        }

        public void Resize(float delta)
        {
            var canvas = view.canvas;
            float max = 480;
            if (canvas)
            {
                max = Math.Max(280, canvas.rect.height - 240);
            }

            panelHeight = Mathf.Clamp(panelHeight + delta, 280, max);
            panel.offsetMin = new Vector2(28, 46);
            panel.offsetMax = new Vector2(-28, 46 + panelHeight);
            if (content)
            {
                content.offsetMin = new Vector2(content.offsetMin.x, 60 + panelHeight);
            }

            if (advanced && canvas)
            {
                float available = canvas.rect.height - panel.offsetMax.y - 58;
                float bottom = Math.Min(6, available - 324);
                var rect = (RectTransform)advanced.transform;
                rect.offsetMin = new Vector2(0, bottom);
                rect.offsetMax = new Vector2(0, bottom + 324);
            }
        }

        private void OnDisable()
        {
            Pause();
        }

        private void OnDestroy()
        {
            loading?.Cancel();
            loading?.Dispose();
            if (observed != null)
            {
                observed.Changed -= Refresh;
            }

            if (source)
            {
                source.Stop();
                source.clip = null;
            }

            lease?.Dispose();
        }
    }
}
