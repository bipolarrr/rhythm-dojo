using System;
using System.Collections.Generic;

namespace RhythmDojo.Gameplay
{
    // Chronological judgment schedule, independent of rendering and editor snap settings.
    internal sealed class RuleSession
    {
        private readonly struct Check
        {
            public readonly int Note, Part;
            public readonly double Time;
            public Check(int note, int part, double time) { Note = note; Part = part; Time = time; }
        }
        private readonly struct InputChange
        {
            public readonly int Lane;
            public readonly double Time;
            public readonly bool Down;
            public InputChange(int lane, double time, bool down) { Lane = lane; Time = time; Down = down; }
        }
        private readonly ChartData chart;
        private readonly PlayRules rules;
        private readonly List<Check> checks = new List<Check>();
        private readonly List<InputChange> history = new List<InputChange>();
        private readonly bool[] heads, tails, held;
        private readonly NoteState[] states;
        private int next;
        public SessionState State { get; private set; }
        public int Perfect { get; private set; }
        public int Good { get; private set; }
        public int Miss { get; private set; }
        public int Combo { get; private set; }
        public int Health { get; private set; }
        public double LastTimingError { get; private set; }
        public FailureSnapshot Failure { get; private set; }
        public SessionSnapshot Snapshot => new SessionSnapshot(State, Perfect, Good, Miss, Combo, checks.Count,
            Health, (double)Health / rules.MaximumHealth, Failure, rules);
        public event Action<JudgmentEvent> Judged;
        public event Action<HoldStartedEvent> HoldStarted;
        public event Action<FailureSnapshot> Failed;
        public RuleSession(ChartData chart, GameModeRules mode, TempoMap tempo, PlayRules rules)
        {
            chart.Validate(mode); rules.Validate();
            this.chart = chart; this.rules = rules.Copy();
            heads = new bool[chart.Count]; tails = new bool[chart.Count];
            states = new NoteState[chart.Count]; held = new bool[mode.LaneCount];
            for (int i = 0; i < chart.Count; i++)
            {
                var note = chart[i];
                checks.Add(new Check(i, 0, note.StartTime + rules.Timing.GoodWindow));
                if (note.Kind != NoteKind.Hold) continue;
                double startBeat = tempo.BeatAtTime(note.StartTime);
                for (int tick = 1; ; tick++)
                {
                    double time = tempo.TimeAtBeat(startBeat + tick * rules.HoldCheckBeats);
                    if (time >= note.EndTime - RhythmSession.TimingTolerance) break;
                    checks.Add(new Check(i, 1, time));
                }
                checks.Add(new Check(i, 2, note.EndTime + (rules.HoldEnd == HoldEndMode.Release ? rules.Timing.GoodWindow : 0)));
            }
            checks.Sort((a, b) =>
            {
                int order = a.Time.CompareTo(b.Time);
                if (order != 0) return order;
                bool aTimeout = a.Part == 0 || (a.Part == 2 && rules.HoldEnd == HoldEndMode.Release);
                bool bTimeout = b.Part == 0 || (b.Part == 2 && rules.HoldEnd == HoldEndMode.Release);
                order = aTimeout.CompareTo(bTimeout);
                return order != 0 ? order : a.Note != b.Note ? a.Note.CompareTo(b.Note) : a.Part.CompareTo(b.Part);
            });
            Reset();
        }
        public bool IsHeld(int lane) => held[lane];
        public NoteState GetState(int index) => states[index];
        public void Reset()
        {
            Array.Clear(heads, 0, heads.Length); Array.Clear(tails, 0, tails.Length);
            Array.Clear(states, 0, states.Length); Array.Clear(held, 0, held.Length);
            history.Clear(); next = Perfect = Good = Miss = Combo = 0;
            Health = rules.StartingHealth; Failure = default; LastTimingError = 0; State = SessionState.Ready;
        }
        public void Start() { Reset(); State = SessionState.Playing; }
        private bool HeldAt(int lane, double time)
        {
            for (int i = history.Count - 1; i >= 0; i--)
                if (history[i].Lane == lane && history[i].Time <= time + RhythmSession.TimingTolerance) return history[i].Down;
            return false;
        }
        public void PressLane(int lane, double time)
        {
            if (State != SessionState.Playing || held[lane]) return;
            Sweep(time, false);
            if (State != SessionState.Playing) return;
            held[lane] = true; history.Add(new InputChange(lane, time, true));
            int best = -1; double nearest = double.MaxValue;
            for (int i = 0; i < chart.Count; i++)
            {
                double error = Math.Abs(time - chart[i].StartTime);
                if (!heads[i] && chart[i].Lane == lane && error <= rules.Timing.GoodWindow + RhythmSession.TimingTolerance && error < nearest)
                { best = i; nearest = error; }
            }
            if (best >= 0)
            {
                heads[best] = true;
                var note = chart[best];
                var grade = JudgmentEvaluator.Grade(time - note.StartTime, rules.Timing);
                states[best] = note.Kind == NoteKind.Hold ? NoteState.Holding : NoteState.Completed;
                Apply(best, grade, time - note.StartTime, time);
                if (note.Kind == NoteKind.Hold) HoldStarted?.Invoke(new HoldStartedEvent(new JudgmentEvent(best, note, grade, time - note.StartTime)));
            }
            Sweep(time, true);
        }
        public void ReleaseLane(int lane, double time)
        {
            if (State != SessionState.Playing) return;
            // A key held through an automatic tail completes it before the release.
            Sweep(time, true);
            if (State != SessionState.Playing) return;
            held[lane] = false; history.Add(new InputChange(lane, time, false));
            for (int i = 0; i < chart.Count; i++)
            {
                var note = chart[i];
                if (note.Kind != NoteKind.Hold || note.Lane != lane || tails[i] || time < note.StartTime) continue;
                double error = time - note.EndTime;
                if (rules.HoldEnd == HoldEndMode.Release && Math.Abs(error) <= rules.Timing.GoodWindow + RhythmSession.TimingTolerance)
                {
                    tails[i] = true;
                    var grade = JudgmentEvaluator.Grade(error, rules.Timing);
                    states[i] = grade == Judgment.Miss ? NoteState.Missed : NoteState.Completed;
                    Apply(i, grade, error, time);
                }
                else if (time < note.EndTime) Combo = 0;
            }
        }
        public void Advance(double time) => Sweep(time, true);
        private void Sweep(double time, bool inclusive)
        {
            while (State == SessionState.Playing && next < checks.Count)
            {
                var check = checks[next];
                if ((check.Part == 0 && heads[check.Note]) || (check.Part == 2 && tails[check.Note])) { next++; continue; }
                // Head/timed-tail windows include their boundary; timeout happens strictly after it.
                bool timeout = check.Part == 0 || (check.Part == 2 && rules.HoldEnd == HoldEndMode.Release);
                if (timeout ? time <= check.Time + RhythmSession.TimingTolerance :
                    inclusive ? time + RhythmSession.TimingTolerance < check.Time : time <= check.Time + RhythmSession.TimingTolerance) break;
                next++;
                int i = check.Note; var note = chart[i];
                if (check.Part == 0)
                {
                    if (heads[i]) continue;
                    heads[i] = true;
                    states[i] = note.Kind == NoteKind.Hold ? NoteState.Holding : NoteState.Missed;
                    Apply(i, Judgment.Miss, check.Time - note.StartTime, check.Time);
                }
                else if (check.Part == 1)
                    Apply(i, HeldAt(note.Lane, check.Time) ? Judgment.Perfect : Judgment.Miss, 0, check.Time);
                else
                {
                    if (tails[i]) continue;
                    tails[i] = true;
                    var grade = rules.HoldEnd == HoldEndMode.Automatic && HeldAt(note.Lane, note.EndTime) ? Judgment.Perfect : Judgment.Miss;
                    states[i] = grade == Judgment.Miss ? NoteState.Missed : NoteState.Completed;
                    Apply(i, grade, check.Time - note.EndTime, check.Time);
                }
            }
            if (State == SessionState.Playing && time >= chart.CompletionTime && Perfect + Good + Miss == checks.Count)
            { State = SessionState.Completed; Array.Clear(held, 0, held.Length); }
        }
        private void Apply(int i, Judgment grade, double error, double time)
        {
            if (grade == Judgment.Perfect) Perfect++; else if (grade == Judgment.Good) Good++; else Miss++;
            Combo = ComboRules.Apply(Combo, grade);
            if (rules.Mode == PlayMode.Gauge) Health = GaugeRules.Apply(Health, Combo, grade, rules, Failure.Failed);
            bool failed = rules.Mode == PlayMode.Gauge ? Health == 0 : ChallengeRules.Exceeded(Perfect, Good, Miss, rules);
            if (failed && !Failure.Failed)
            {
                bool continues = rules.Mode == PlayMode.Gauge && rules.GaugeFailure == GaugeFailureMode.Continue;
                Failure = new FailureSnapshot(time, rules.Mode == PlayMode.Gauge ? FailureReason.GaugeEmpty : FailureReason.ChallengeLimit, continues);
                if (!continues) { State = SessionState.GameOver; Array.Clear(held, 0, held.Length); }
                Failed?.Invoke(Failure);
            }
            LastTimingError = error;
            Judged?.Invoke(new JudgmentEvent(i, chart[i], grade, error));
        }
    }
}
