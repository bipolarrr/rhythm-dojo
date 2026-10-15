using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using System.Threading;
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
        private ChartEditorScreen screen;
        private ChartBeatGrid grid;
        private EditorSession observed;
        private SongProject snapshot;
        private FileSongProjectStore store;
        private AudioSource source;
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
        public double Duration => source?.clip?.length ?? 0;
        public bool IsBusy => screen.IsBusy || loadingAudio;

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

        public void Initialize(ChartEditorScreen screen, ChartBeatGrid grid, FileSongProjectStore store)
        {
            this.screen = screen;
            this.grid = grid;
            var canvas = screen.GetComponentInParent<Canvas>();
            if (!canvas)
            {
                enabled = false;
                return;
            }

            this.store = store;
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0;
            source.volume = 0.65f;
            content = screen.transform.parent?.parent as RectTransform;
            if (content && content.parent != canvas.transform)
            {
                content = null;
            }

            if (content)
            {
                var preview = screen.transform.parent;
                if (!preview.GetComponent<RectMask2D>())
                {
                    preview.gameObject.AddComponent<RectMask2D>();
                }

                var help = preview.GetComponentsInChildren<Text>().FirstOrDefault(label => label.name == "Help");
                if (help)
                {
                    help.verticalOverflow = VerticalWrapMode.Truncate;
                    help.rectTransform.anchorMin = Vector2.zero;
                    help.rectTransform.anchorMax = new Vector2(0, 1);
                    help.rectTransform.offsetMin = new Vector2(18, 90);
                    help.rectTransform.offsetMax = new Vector2(398, -18);
                }

                foreach (var button in content.GetComponentsInChildren<Button>())
                {
                    var rect = (RectTransform)button.transform;
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.zero;
                    rect.pivot = Vector2.zero;
                    rect.anchoredPosition = new Vector2(18, 18);
                    rect.SetAsLastSibling();
                }
            }

            BuildUi(canvas.transform);
            var menu = canvas.GetComponent<ChartEditorMenu>();
            if (menu)
            {
                menu.MoveStatusToFooter();
            }

            Observe();
        }

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
                loaded = await ChartEditorCompositionRoot.LoadAudio(
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
            WindowDuration = Math.Max(0.02, Math.Min(Math.Max(8, Duration),
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
                ApplyOrigin(points, detail.FirstVisibleGridTime + AudioOffset);
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

        private void BuildUi(Transform parent)
        {
            panel = AudioTimelineUi.Panel(parent, "Audio Timeline", new Color(0.075f, 0.09f, 0.12f));
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = new Vector2(1, 0);
            panel.pivot = Vector2.zero;
            Resize(0);
            var resize = AudioTimelineUi.Panel(panel, "Resize Timeline", new Color(0.23f, 0.3f, 0.38f));
            AudioTimelineUi.Place(resize, 0, 0, 0, 5);
            resize.anchorMax = Vector2.one;
            resize.anchorMin = new Vector2(0, 1);
            resize.offsetMin = new Vector2(0, -5);
            resize.offsetMax = Vector2.zero;
            resize.gameObject.AddComponent<AudioTimelineResize>().Owner = this;
            playLabel = AudioTimelineUi.Button(panel, "Play Audio", "재생 / Play", 12, 10, 130,
                () =>
                {
                    if (playing)
                    {
                        Pause();
                    }
                    else
                    {
                        Play();
                    }
                }).GetComponentInChildren<Text>();
            AudioTimelineUi.Button(panel, "Follow Audio", "재생 따라가기", 150, 10, 140,
                () =>
                {
                    follow = true;
                    SyncChart(MusicTime);
                });
            clock = AudioTimelineUi.Label(panel, "Audio Clock", "", 305, 10, 280);
            ruler = AudioTimelineUi.Label(panel, "Musical Position", "", 590, 10, 630);
            overview = AudioTimelineUi.Wave(panel, "Full Song Waveform", 12, 48, 36, this, true);
            detail = AudioTimelineUi.Wave(panel, "Detailed Waveform", 12, 90, 72, this, false);
            detail.rectTransform.anchorMin = Vector2.zero;
            detail.rectTransform.offsetMin = new Vector2(12, 118);
            bpm = AudioTimelineUi.Field(panel, "Grid BPM", "BPM", 12, 173, 76, "120");
            origin = AudioTimelineUi.Field(panel, "Grid Origin", "첫 박 위치 ms (+뒤로)", 96, 173, 145, "0");
            AudioTimelineUi.Button(panel, "Apply Grid", "적용", 249, 192, 55, ApplyBasic);
            AudioTimelineUi.Button(panel, "Cursor Origin", "그리드 수정", 310, 192, 112, SetOrigin);
            nudge = AudioTimelineUi.Field(panel, "Grid Nudge", "미세 이동량", 430, 173, 75, "1");
            nudgeUnit = AudioTimelineUi.Dropdown(panel, "Nudge Units", 512, 192, 65, new[] { "ms", "박" }, _ => { });
            AudioTimelineUi.Button(panel, "Nudge Earlier", "←", 583, 192, 36, () => Shift(-1));
            AudioTimelineUi.Button(panel, "Nudge Later", "→", 625, 192, 36, () => Shift(1));
            AudioTimelineUi.Button(panel, "Double BPM", "×2", 667, 192, 43, () => ScaleBpm(2));
            AudioTimelineUi.Button(panel, "Half BPM", "÷2", 716, 192, 43, () => ScaleBpm(0.5));
            AudioTimelineUi.Button(panel, "Grid Lock", "잠금 OFF", 767, 192, 95, () =>
            {
                locked = !locked;
            }).onClick.AddListener(() => UpdateToggleLabel("Grid Lock", locked, "잠금"));
            AudioTimelineUi.Dropdown(panel, "Note Snap", 870, 192, 90,
                new[] { "1/4", "1/8", "1/16", "1/32", "1/8T", "1/16T", "OFF" }, value =>
                {
                    screen.SnapEnabled = value != 6;
                    if (value < 6)
                    {
                        screen.SnapBeats = new[] { 1.0, 0.5, 0.25, 0.125, 1.0 / 3, 1.0 / 6 }[value];
                    }
                });
            message = AudioTimelineUi.Label(panel, "Timeline Status", "", 12, 234, 1200);
            tooltip = AudioTimelineUi.Label(panel, "Timeline Tooltip", "", 12, 258, 1200, 16);
            tooltip.fontSize = 12;
            foreach (RectTransform rect in panel)
            {
                if (rect.anchorMin.y != 1 || rect.anchorMax.y != 1)
                {
                    continue;
                }

                if (-rect.anchoredPosition.y < 173)
                {
                    continue;
                }

                rect.anchorMin = new Vector2(rect.anchorMin.x, 0);
                rect.anchorMax = new Vector2(rect.anchorMax.x, 0);
                rect.anchoredPosition += new Vector2(0, panelHeight);
            }

            BuildAdvanced();
            Resize(0);
            foreach (var button in panel.GetComponentsInChildren<Button>(true))
            {
                var hint = button.gameObject.AddComponent<AudioTimelineTooltip>();
                hint.Target = tooltip;
                hint.Caption = button.GetComponentInChildren<Text>().text;
                switch (button.name)
                {
                    case "Cursor Origin":
                        hint.Caption = "상세 파형에서 가장 왼쪽에 보이는 그리드선을 첫 마디 첫 박으로 지정합니다.";
                        break;
                    case "Double BPM":
                    case "Half BPM":
                        hint.Caption = "모든 템포 구간의 BPM을 배수로 조정합니다.";
                        break;
                    case "Nudge Earlier":
                    case "Nudge Later":
                        hint.Caption = "그리드 첫 박을 이동합니다. +는 음원 뒤쪽, −는 앞쪽입니다.";
                        break;
                    case "Apply Tempo Segment":
                        hint.Caption = "선택한 템포 구간의 시작 시점, BPM, 박자표를 함께 적용합니다.";
                        break;
                }
            }
        }

        private void UpdateToggleLabel(string name, bool value, string caption)
        {
            string state = " OFF";
            if (value)
            {
                state = " ON";
            }

            panel.GetComponentsInChildren<Button>(true).First(b => b.name == name)
                .GetComponentInChildren<Text>().text = caption + state;
        }

        private void BuildAdvanced()
        {
            var rect = AudioTimelineUi.Panel(panel, "Advanced Grid Panel", new Color(0.09f, 0.115f, 0.155f));
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(0, 0);
            rect.offsetMin = new Vector2(0, 6);
            rect.offsetMax = new Vector2(0, 330);
            advanced = rect.gameObject;
            AudioTimelineUi.Label(rect, "Advanced Heading", "고급 GRID · 시간은 음원상의 초 · 첫 변경점은 채보 시간 0 고정", 16, 10, 1050);
            AudioTimelineUi.Button(rect, "Close Advanced", "닫기", 1102, 10, 110, () => advanced.SetActive(false));
            segments = AudioTimelineUi.Dropdown(rect, "Tempo Segments", 16, 48, 350, new[] { "0" }, value =>
            {
                if (!filling)
                {
                    selected = value;
                    FillFields();
                }
            });
            segmentTime = AudioTimelineUi.Field(rect, "Segment Time", "변경점 위치 s", 16, 88, 166, "0");
            segmentBpm = AudioTimelineUi.Field(rect, "Segment BPM", "구간 BPM", 190, 88, 176, "120");
            numerator = AudioTimelineUi.Field(rect, "Meter Numerator", "박자표 분자", 16, 148, 166, "4");
            denominator = AudioTimelineUi.Field(rect, "Meter Denominator", "박자표 분모", 190, 148, 176, "4");
            AudioTimelineUi.Button(rect, "Add Tempo Segment", "커서에 구간 추가", 16, 218, 170, () => ChangeSegment(true));
            AudioTimelineUi.Button(rect, "Apply Tempo Segment", "선택 구간 적용 / 이동", 194, 218, 172, () => ChangeSegment(false));
            AudioTimelineUi.Button(rect, "Delete Tempo Segment", "선택 구간 삭제", 16, 266, 170, DeleteSegment);
            AudioTimelineUi.Button(rect, "Align Tempo Segment", "커서 = 구간 첫 박", 194, 266, 172, AlignSegment);
            AudioTimelineUi.Label(rect, "BPM Calculator Heading", "두 실제 박 위치로 BPM 계산 (4분음표 기준)", 400, 48, 395);
            firstBeat = AudioTimelineUi.Field(rect, "First Actual Beat", "A: 실제 박 s", 400, 88, 140, "0");
            secondBeat = AudioTimelineUi.Field(rect, "Second Actual Beat", "B: 실제 박 s", 548, 88, 140, "1");
            AudioTimelineUi.Button(rect, "Capture First Beat", "커서 → A", 400, 148, 140,
                () => firstBeat.text = MusicTime.ToString("R", CultureInfo.InvariantCulture));
            AudioTimelineUi.Button(rect, "Capture Second Beat", "커서 → B", 548, 148, 140,
                () => secondBeat.text = MusicTime.ToString("R", CultureInfo.InvariantCulture));
            beatCount = AudioTimelineUi.Field(rect, "Beat Interval Count", "A–B 사이 박 간격 수", 400, 195, 180, "2");
            AudioTimelineUi.Button(rect, "Calculate BPM", "BPM 계산 → 구간 입력", 400, 266, 288, CalculateBpm);
            AudioTimelineUi.Label(rect, "Note Policy Heading", "기존 노트 처리", 730, 48, 455);
            AudioTimelineUi.Dropdown(rect, "Existing Note Policy", 730, 88, 465,
                new[] { "기본: 노트 음악 시간 유지", "노트 박자 유지 (노트 시간 변경)" }, value => preserveBeats = value == 1);
            AudioTimelineUi.Label(rect, "Grid Density Heading", "표시 밀도 (노트 스냅과 별도)", 730, 148, 465);
            AudioTimelineUi.Dropdown(rect, "Display Density", 730, 185, 220,
                new[] { "1/4", "1/8", "1/16", "1/32", "1/8T", "1/16T" }, value =>
                {
                    DisplayStep = new[] { 1.0, 0.5, 0.25, 0.125, 1.0 / 3, 1.0 / 6 }[value];
                    Redraw();
                }).SetValueWithoutNotify(2);
            AudioTimelineUi.Label(rect, "Note Policy Help",
                "롱노트 양 끝과 모든 채보에 동일 적용\n자동 스냅 없음 · Undo/Redo로 복원\n오디오 오프셋은 기존 설정 메뉴에서 별도 편집",
                730, 234, 465, 74);
            advanced.SetActive(false);
        }

        public void Resize(float delta)
        {
            var canvas = GetComponentInParent<Canvas>();
            float max = 480;
            if (canvas)
            {
                max = Math.Max(280, ((RectTransform)canvas.transform).rect.height - 240);
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
                float available = ((RectTransform)canvas.transform).rect.height - panel.offsetMax.y - 58;
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
            if (panel)
            {
                Destroy(panel.gameObject);
            }
        }
    }
}
