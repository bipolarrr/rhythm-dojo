using System;
using System.Collections.Generic;

namespace RhythmDojo.Gameplay
{
    // Chronological judgment schedule, independent of rendering and editor snap settings.
    internal sealed class RuleSession
    {
        private readonly struct Check
        {
            public readonly int Note,
                Part;
            public readonly double Time;

            public Check(int note, int part, double time)
            {
                Note = note;
                Part = part;
                Time = time;
            }
        }

        private readonly struct InputChange
        {
            public readonly int Lane;
            public readonly double Time;
            public readonly bool Down;

            public InputChange(int lane, double time, bool down)
            {
                Lane = lane;
                Time = time;
                Down = down;
            }
        }

        private readonly ChartData chart;
        private readonly PlayRules rules;
        private readonly List<Check> checks = new List<Check>();
        private readonly List<InputChange> history = new List<InputChange>();
        private readonly bool[] heads,
            tails,
            held;
        private readonly NoteState[] states;
        private int next;
        private readonly NoteJudgmentRecord[] records;
        private readonly bool[] broken;

        public SessionState State { get; private set; }
        public int Perfect { get; private set; }
        public int Good { get; private set; }
        public int Miss { get; private set; }
        public int Combo { get; private set; }
        public int Health { get; private set; }
        public double LastTimingError { get; private set; }
        public FailureSnapshot Failure { get; private set; }

        public SessionSnapshot Snapshot =>
            new SessionSnapshot(
                State,
                Perfect,
                Good,
                Miss,
                Combo,
                checks.Count,
                Health,
                (double)Health / rules.MaximumHealth,
                Failure,
                rules
            );

        public event Action<JudgmentEvent> Judged;
        public event Action<HoldStartedEvent> HoldStarted;
        public event Action<FailureSnapshot> Failed;

        public RuleSession(ChartData chart, GameModeRules mode, TempoMap tempo, PlayRules rules, NoteJudgmentRecord[] records)
        {
            chart.Validate(mode);
            rules.Validate();
            this.chart = chart;
            this.rules = rules.Copy();
            this.records = records;
            broken = new bool[chart.Count];
            heads = new bool[chart.Count];
            tails = new bool[chart.Count];
            states = new NoteState[chart.Count];
            held = new bool[mode.LaneCount];

            for (int i = 0; i < chart.Count; i++)
            {
                var note = chart[i];
                checks.Add(new Check(i, 0, note.StartTime + rules.Timing.GoodWindow));

                if (note.Kind != NoteKind.Hold)
                {
                    continue;
                }

                double startBeat = tempo.BeatAtTime(note.StartTime);

                for (int tick = 1; ; tick++)
                {
                    double time = tempo.TimeAtBeat(startBeat + tick * rules.HoldCheckBeats);

                    if (time >= note.EndTime - RhythmSession.TimingTolerance)
                    {
                        break;
                    }

                    checks.Add(new Check(i, 1, time));
                }

                checks.Add(
                    new Check(
                        i,
                        2,
                        note.EndTime
                            + (
                                rules.HoldEnd switch
                                {
                                    HoldEndMode.Release => rules.Timing.GoodWindow,
                                    _ => 0,
                                }
                            )
                    )
                );
            }

            checks.Sort(
                (a, b) =>
                {
                    int order = a.Time.CompareTo(b.Time);

                    if (order != 0)
                    {
                        return order;
                    }

                    bool aTimeout =
                        a.Part == 0 || a.Part == 2 && rules.HoldEnd == HoldEndMode.Release;
                    bool bTimeout =
                        b.Part == 0 || b.Part == 2 && rules.HoldEnd == HoldEndMode.Release;
                    order = aTimeout.CompareTo(bTimeout);

                    if (order != 0)
                    {
                        return order;
                    }
                    else if (a.Note != b.Note)
                    {
                        return a.Note.CompareTo(b.Note);
                    }
                    else
                    {
                        return a.Part.CompareTo(b.Part);
                    }
                }
            );
            Reset();
        }

        public bool IsHeld(int lane) => held[lane];

        public NoteState GetState(int index) => states[index];

        public NoteJudgmentRecord GetNoteJudgments(int index) => records[index];

        public void Reset()
        {
            Array.Clear(records, 0, records.Length);
            Array.Clear(broken, 0, broken.Length);
            Array.Clear(heads, 0, heads.Length);
            Array.Clear(tails, 0, tails.Length);
            Array.Clear(states, 0, states.Length);
            Array.Clear(held, 0, held.Length);
            history.Clear();
            next = Perfect = Good = Miss = Combo = 0;
            Health = rules.StartingHealth;
            Failure = default;
            LastTimingError = 0;
            State = SessionState.Ready;
        }

        public void Start()
        {
            Reset();
            State = SessionState.Playing;
        }

        private bool HeldAt(int lane, double time)
        {
            for (int i = history.Count - 1; i >= 0; i--)
            {
                if (
                    history[i].Lane == lane
                    && history[i].Time <= time + RhythmSession.TimingTolerance
                )
                {
                    return history[i].Down;
                }
            }

            return false;
        }

        public void PressLane(int lane, double time)
        {
            if (State != SessionState.Playing || held[lane])
            {
                return;
            }

            Sweep(time, false);

            if (State != SessionState.Playing)
            {
                return;
            }

            held[lane] = true;
            history.Add(new InputChange(lane, time, true));

            int best = -1;
            double nearest = double.MaxValue;

            for (int i = 0; i < chart.Count; i++)
            {
                double error = Math.Abs(time - chart[i].StartTime);

                if (!heads[i] && chart[i].Lane == lane)
                {
                    if (
                        error <= rules.Timing.GoodWindow + RhythmSession.TimingTolerance
                        && error < nearest
                    )
                    {
                        best = i;
                        nearest = error;
                    }
                }
            }

            if (best >= 0)
            {
                heads[best] = true;

                var note = chart[best];
                var grade = JudgmentEvaluator.Grade(time - note.StartTime, rules.Timing);

                if (note.Kind == NoteKind.Hold)
                {
                    states[best] = NoteState.Holding;
                }
                else
                {
                    states[best] = NoteState.Completed;
                }

                Apply(best, grade, time - note.StartTime, time, 0);

                if (note.Kind == NoteKind.Hold)
                {
                    HoldStarted?.Invoke(
                        new HoldStartedEvent(
                            new JudgmentEvent(best, note, grade, time - note.StartTime)
                        )
                    );
                }
            }

            Sweep(time, true);
        }

        public void ReleaseLane(int lane, double time)
        {
            if (State != SessionState.Playing)
            {
                return;
            }

            // A key held through an automatic tail completes it before the release.
            Sweep(time, true);

            if (State != SessionState.Playing)
            {
                return;
            }

            held[lane] = false;
            history.Add(new InputChange(lane, time, false));

            for (int i = 0; i < chart.Count; i++)
            {
                var note = chart[i];

                if (State != SessionState.Playing)
                {
                    break;
                }

                if (note.Kind != NoteKind.Hold || note.Lane != lane)
                {
                    continue;
                }

                if (tails[i] || broken[i])
                {
                    continue;
                }

                if (time < note.StartTime)
                {
                    continue;
                }

                double error = time - note.EndTime;

                if (
                    rules.HoldEnd == HoldEndMode.Release
                    && Math.Abs(error) <= rules.Timing.GoodWindow + RhythmSession.TimingTolerance
                )
                {
                    tails[i] = true;

                    var grade = JudgmentEvaluator.Grade(error, rules.Timing);

                    if (grade == Judgment.Miss)
                    {
                        states[i] = NoteState.Missed;
                    }
                    else
                    {
                        states[i] = NoteState.Completed;
                    }

                    Apply(i, grade, error, time, 2);
                }
                else if (time < note.EndTime)
                {
                    if (rules.HoldEnd == HoldEndMode.Release && heads[i])
                    {
                        broken[i] = true;
                        tails[i] = true;
                        states[i] = NoteState.Missed;
                        Apply(i, Judgment.Miss, error, time, 2);
                    }
                    else
                    {
                        Combo = 0;
                    }
                }
            }
        }

        public void Advance(double time) => Sweep(time, true);

        private void Sweep(double time, bool inclusive)
        {
            while (State == SessionState.Playing && next < checks.Count)
            {
                var check = checks[next];

                switch (check.Part)
                {
                    case 0 when heads[check.Note]:
                    case 2 when tails[check.Note]:
                        next++;
                        continue;
                }

                // Head/timed-tail windows include their boundary; timeout happens strictly after it.
                bool timeout =
                    check.Part == 0 || check.Part == 2 && rules.HoldEnd == HoldEndMode.Release;

                if (timeout || !inclusive)
                {
                    if (time <= check.Time + RhythmSession.TimingTolerance)
                    {
                        break;
                    }
                }
                else if (time + RhythmSession.TimingTolerance < check.Time)
                {
                    break;
                }

                next++;

                int i = check.Note;
                var note = chart[i];

                if (check.Part == 0)
                {
                    if (heads[i])
                    {
                        continue;
                    }

                    heads[i] = true;

                    if (note.Kind == NoteKind.Hold)
                    {
                        states[i] = NoteState.Holding;
                    }
                    else
                    {
                        states[i] = NoteState.Missed;
                    }

                    if (note.Kind == NoteKind.Hold && rules.HoldEnd == HoldEndMode.Release)
                    {
                        broken[i] = true;
                    }

                    Apply(i, Judgment.Miss, check.Time - note.StartTime, check.Time, 0);
                }
                else if (check.Part == 1)
                {
                    if (!broken[i] && HeldAt(note.Lane, check.Time))
                    {
                        Apply(i, Judgment.Perfect, 0, check.Time);
                    }
                    else
                    {
                        Apply(i, Judgment.Miss, 0, check.Time);
                    }
                }
                else
                {
                    if (tails[i])
                    {
                        continue;
                    }

                    tails[i] = true;

                    Judgment grade;

                    if (rules.HoldEnd == HoldEndMode.Automatic && HeldAt(note.Lane, note.EndTime))
                    {
                        grade = Judgment.Perfect;
                    }
                    else
                    {
                        grade = Judgment.Miss;
                    }

                    if (grade == Judgment.Miss)
                    {
                        states[i] = NoteState.Missed;
                    }
                    else
                    {
                        states[i] = NoteState.Completed;
                    }

                    Apply(i, grade, check.Time - note.EndTime, check.Time, 2);
                }
            }

            if (State == SessionState.Playing)
            {
                if (time >= chart.CompletionTime && Perfect + Good + Miss == checks.Count)
                {
                    State = SessionState.Completed;
                    Array.Clear(held, 0, held.Length);
                }
            }
        }

        private void Apply(int i, Judgment grade, double error, double time, int part = 1)
        {
            var judgment = new JudgmentEvent(i, chart[i], grade, error);

            if (part == 0 && !records[i].Start.HasValue)
            {
                records[i] = new NoteJudgmentRecord(judgment, records[i].End);
            }
            else if (part == 2 && !records[i].End.HasValue)
            {
                records[i] = new NoteJudgmentRecord(records[i].Start, judgment);
            }

            if (grade == Judgment.Perfect)
            {
                Perfect++;
            }
            else if (grade == Judgment.Good)
            {
                Good++;
            }
            else
            {
                Miss++;
            }

            Combo = ComboRules.Apply(Combo, grade);

            if (rules.Mode == PlayMode.Gauge)
            {
                Health = GaugeRules.Apply(Health, Combo, grade, rules, Failure.Failed);
            }

            bool failed;

            if (rules.Mode == PlayMode.Gauge)
            {
                failed = Health == 0;
            }
            else
            {
                failed = ChallengeRules.Exceeded(Perfect, Good, Miss, rules);
            }

            if (failed && !Failure.Failed)
            {
                bool continues =
                    rules.Mode == PlayMode.Gauge && rules.GaugeFailure == GaugeFailureMode.Continue;
                Failure = new FailureSnapshot(
                    time,
                    (
                        rules.Mode switch
                        {
                            PlayMode.Gauge => FailureReason.GaugeEmpty,
                            _ => FailureReason.ChallengeLimit,
                        }
                    ),
                    continues
                );

                if (!continues)
                {
                    State = SessionState.GameOver;
                    Array.Clear(held, 0, held.Length);
                }

                Failed?.Invoke(Failure);
            }

            LastTimingError = error;
            Judged?.Invoke(new JudgmentEvent(i, chart[i], grade, error));
        }
    }
}
