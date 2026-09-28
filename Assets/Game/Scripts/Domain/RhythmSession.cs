using System;

namespace RhythmDojo.Gameplay
{
    public enum Judgment { Perfect, Good, Miss }
    public enum NoteState { Pending, Holding, Completed, Missed }
    public enum SessionState { Ready, Playing, Completed }

    // Pure lane/time simulation. No transforms, input devices, audio or editor APIs.
    public sealed class RhythmSession
    {
        public const double TimingTolerance = 1e-9;
        private readonly ChartData chart;
        private readonly JudgmentSettings config;
        private readonly NoteState[] states;
        private readonly Judgment[] heads;
        private readonly int[] active;
        private readonly bool[] held;
        public int LaneCount => held.Length;
        public SessionSnapshot Snapshot => new SessionSnapshot(State, Perfect, Good, Miss, Combo, chart.Count);
        public SessionState State { get; private set; }
        public int Perfect { get; private set; }
        public int Good { get; private set; }
        public int Miss { get; private set; }
        public int Combo { get; private set; }
        public int Resolved => Perfect + Good + Miss;
        public double LastTimingError { get; private set; }
        public event Action<JudgmentEvent> Judged;
        public event Action<HoldStartedEvent> HoldStarted;
        public NoteState GetState(int index) => states[index];
        public bool IsHeld(int lane) => held[lane];

        public RhythmSession(ChartData chart, JudgmentSettings config, GameModeRules mode)
        {
            if (chart == null) throw new ArgumentNullException(nameof(chart));
            chart.Validate(mode); config.Validate();
            this.chart = chart; this.config = config;
            states = new NoteState[chart.Count]; heads = new Judgment[chart.Count];
            active = new int[mode.LaneCount]; held = new bool[mode.LaneCount]; Reset();
        }
        public void Reset()
        {
            Array.Clear(states, 0, states.Length); Array.Clear(heads, 0, heads.Length);
            Array.Clear(held, 0, held.Length);
            for (int i = 0; i < active.Length; i++) active[i] = -1;
            Perfect = Good = Miss = Combo = 0; LastTimingError = 0; State = SessionState.Ready;
        }
        public void Start() { Reset(); State = SessionState.Playing; }
        public Judgment Grade(double error) => Math.Abs(error) <= config.PerfectWindow + TimingTolerance
            ? Judgment.Perfect : Math.Abs(error) <= config.GoodWindow + TimingTolerance ? Judgment.Good : Judgment.Miss;
        private void ValidateInput(int lane, double time)
        {
            if (lane < 0 || lane >= LaneCount || !double.IsFinite(time)) throw new ArgumentOutOfRangeException("Invalid lane or song timestamp.");
        }
        public void PressLane(int lane, double songTime)
        {
            ValidateInput(lane, songTime);
            if (State != SessionState.Playing || held[lane]) return;
            Advance(songTime);
            if (State != SessionState.Playing) return;
            held[lane] = true;
            if (active[lane] >= 0) return;
            int best = -1; double nearest = double.MaxValue;
            for (int i = 0; i < chart.Count; i++)
            {
                var n = chart[i]; double error = Math.Abs(songTime - n.StartTime);
                if (states[i] == NoteState.Pending && n.Lane == lane && error <= config.GoodWindow + TimingTolerance && error < nearest - TimingTolerance)
                { best = i; nearest = error; }
            }
            if (best < 0) return; // Empty presses do not affect combo.
            double signed = songTime - chart[best].StartTime;
            var grade = Grade(signed); LastTimingError = signed;
            if (chart[best].Kind == NoteKind.Tap) Resolve(best, grade, signed);
            else
            {
                states[best] = NoteState.Holding; heads[best] = grade; active[lane] = best;
                HoldStarted?.Invoke(new HoldStartedEvent(new JudgmentEvent(best, chart[best], grade, signed)));
            }
        }
        public void ReleaseLane(int lane, double songTime)
        {
            ValidateInput(lane, songTime);
            if (State != SessionState.Playing) return;
            // Advance first: a release after the late tail window cannot downgrade a completed hold.
            Advance(songTime); held[lane] = false;
            int i = active[lane]; if (i < 0) return;
            double error = songTime - chart[i].EndTime;
            Resolve(i, (Judgment)Math.Max((int)heads[i], (int)Grade(error)), error);
        }
        public void Advance(double songTime)
        {
            if (!double.IsFinite(songTime)) throw new ArgumentOutOfRangeException(nameof(songTime));
            if (State != SessionState.Playing) return;
            for (int i = 0; i < chart.Count; i++)
            {
                var n = chart[i];
                if (states[i] == NoteState.Pending && songTime > n.StartTime + config.GoodWindow + TimingTolerance)
                    Resolve(i, Judgment.Miss, songTime - n.StartTime);
                else if (states[i] == NoteState.Holding && songTime > n.EndTime + config.GoodWindow + TimingTolerance)
                    Resolve(i, heads[i], songTime - n.EndTime);
            }
            if (songTime >= chart.CompletionTime && Resolved == chart.Count)
            { State = SessionState.Completed; Array.Clear(held, 0, held.Length); }
        }
        private void Resolve(int i, Judgment grade, double error)
        {
            if (states[i] == NoteState.Completed || states[i] == NoteState.Missed) return;
            if (active[chart[i].Lane] == i) active[chart[i].Lane] = -1;
            states[i] = grade == Judgment.Miss ? NoteState.Missed : NoteState.Completed;
            if (grade == Judgment.Perfect) Perfect++; else if (grade == Judgment.Good) Good++; else Miss++;
            Combo = grade == Judgment.Miss ? 0 : Combo + 1;
            LastTimingError = error; Judged?.Invoke(new JudgmentEvent(i, chart[i], grade, error));
        }
    }
}
