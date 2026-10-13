using System;

namespace RhythmDojo.Gameplay
{
    public enum Judgment { Perfect, Good, Miss }
    public enum NoteState { Pending, Holding, Completed, Missed }
    public enum SessionState { Ready, Playing, Completed, GameOver }

    // Pure lane/time simulation. No transforms, input devices, audio or editor APIs.
    public sealed class RhythmSession
    {
        public const double TimingTolerance = 1e-9;
        public const int MaximumHealth = 100;
        public const int PerfectHealthRecovery = 2;
        public const int GoodHealthRecovery = 1;
        public const int MissHealthDamage = 10;
        public int Health { get; private set; }
        private readonly HardModeHealthRules hardModeRules;
        private readonly RuleSession ruleSession;
        public FailureSnapshot Failure => ruleSession?.Failure ?? default;
        public event Action<FailureSnapshot> Failed;
        private double remainingHealth;
        private readonly ChartData chart;
        private readonly JudgmentSettings config;
        private readonly NoteState[] states;
        private readonly Judgment[] heads;
        private readonly int[] active;
        private readonly bool[] held;
        public int LaneCount => held.Length;
        public SessionSnapshot Snapshot => ruleSession?.Snapshot ?? new SessionSnapshot(State, Perfect, Good, Miss, Combo, chart.Count, Health, remainingHealth / MaximumHealth);
        public SessionState State { get; private set; }
        public int Perfect { get; private set; }
        public int Good { get; private set; }
        public int Miss { get; private set; }
        public int Combo { get; private set; }
        public int Resolved => Perfect + Good + Miss;
        public double LastTimingError { get; private set; }
        public event Action<JudgmentEvent> Judged;
        public event Action<HoldStartedEvent> HoldStarted;
        public NoteState GetState(int index) => ruleSession?.GetState(index) ?? states[index];
        public bool IsHeld(int lane) => ruleSession?.IsHeld(lane) ?? held[lane];

        public RhythmSession(ChartData chart, GameModeRules mode, TempoMap tempo, PlayRules rules)
            : this(chart, rules.Timing, mode)
        {
            ruleSession = new RuleSession(chart, mode, tempo, rules);
            ruleSession.Judged += result => { SyncRules(); Judged?.Invoke(result); };
            ruleSession.HoldStarted += result => HoldStarted?.Invoke(result);
            ruleSession.Failed += result => { SyncRules(); Failed?.Invoke(result); };
            SyncRules();
        }
        private void SyncRules()
        {
            State = ruleSession.State; Perfect = ruleSession.Perfect; Good = ruleSession.Good;
            Miss = ruleSession.Miss; Combo = ruleSession.Combo; Health = ruleSession.Health;
            LastTimingError = ruleSession.LastTimingError;
        }

        public RhythmSession(ChartData chart, JudgmentSettings config, GameModeRules mode)
            : this(chart, config, mode, null) { }
        public RhythmSession(ChartData chart, JudgmentSettings config, GameModeRules mode, HardModeHealthRules healthRules)
        {
            hardModeRules = healthRules;
            if (chart == null) throw new ArgumentNullException(nameof(chart));
            chart.Validate(mode); config.Validate();
            this.chart = chart; this.config = config;
            states = new NoteState[chart.Count]; heads = new Judgment[chart.Count];
            active = new int[mode.LaneCount]; held = new bool[mode.LaneCount]; Reset();
        }
        public void Reset()
        {
            if (ruleSession != null) { ruleSession.Reset(); SyncRules(); return; }
            Array.Clear(states, 0, states.Length); Array.Clear(heads, 0, heads.Length);
            Array.Clear(held, 0, held.Length);
            for (int i = 0; i < active.Length; i++) active[i] = -1;
            Health = MaximumHealth; remainingHealth = MaximumHealth;
            Perfect = Good = Miss = Combo = 0; LastTimingError = 0; State = SessionState.Ready;
        }
        public void Start() { if (ruleSession != null) { ruleSession.Start(); SyncRules(); return; } Reset(); State = SessionState.Playing; }
        public Judgment Grade(double error) => Math.Abs(error) <= config.PerfectWindow + TimingTolerance
            ? Judgment.Perfect : Math.Abs(error) <= config.GoodWindow + TimingTolerance ? Judgment.Good : Judgment.Miss;
        private void ValidateInput(int lane, double time)
        {
            if (lane < 0 || lane >= LaneCount || !double.IsFinite(time)) throw new ArgumentOutOfRangeException("Invalid lane or song timestamp.");
        }
        public void PressLane(int lane, double songTime)
        {
            ValidateInput(lane, songTime);
            if (ruleSession != null) { ruleSession.PressLane(lane, songTime); SyncRules(); return; }
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
            if (ruleSession != null) { ruleSession.ReleaseLane(lane, songTime); SyncRules(); return; }
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
            if (ruleSession != null) { ruleSession.Advance(songTime); SyncRules(); return; }
            if (State != SessionState.Playing) return;
            for (int i = 0; i < chart.Count; i++)
            {
                if (State != SessionState.Playing) break;
                var n = chart[i];
                if (states[i] == NoteState.Pending && songTime > n.StartTime + config.GoodWindow + TimingTolerance)
                    Resolve(i, Judgment.Miss, songTime - n.StartTime);
                else if (states[i] == NoteState.Holding && songTime > n.EndTime + config.GoodWindow + TimingTolerance)
                    Resolve(i, heads[i], songTime - n.EndTime);
            }
            if (State == SessionState.Playing && songTime >= chart.CompletionTime && Resolved == chart.Count)
            { State = SessionState.Completed; Array.Clear(held, 0, held.Length); }
        }
        private void Resolve(int i, Judgment grade, double error)
        {
            if (State != SessionState.Playing || states[i] == NoteState.Completed || states[i] == NoteState.Missed) return;
            if (active[chart[i].Lane] == i) active[chart[i].Lane] = -1;
            states[i] = grade == Judgment.Miss ? NoteState.Missed : NoteState.Completed;
            if (grade == Judgment.Perfect) Perfect++; else if (grade == Judgment.Good) Good++; else Miss++;
            Combo = grade == Judgment.Miss ? 0 : Combo + 1;
            if (hardModeRules == null)
            {
                Health = Math.Clamp(Health + (grade == Judgment.Perfect ? PerfectHealthRecovery :
                    grade == Judgment.Good ? GoodHealthRecovery : -MissHealthDamage), 0, MaximumHealth);
                remainingHealth = Health;
            }
            else
            {
                var rule = hardModeRules.GetRule(grade);
                if (rule.Action == HealthRuleAction.GameOver) remainingHealth = 0;
                else if (rule.Action == HealthRuleAction.Damage) remainingHealth -= (double)MaximumHealth / rule.LossCount;
                // A fractional loss must not round to zero before the configured hit count.
                if (remainingHealth < 1e-8) remainingHealth = 0;
                remainingHealth = Math.Clamp(remainingHealth, 0, MaximumHealth);
                Health = (int)Math.Ceiling(Math.Max(0, remainingHealth - 1e-8));
            }
            if (Health == 0)
            {
                State = SessionState.GameOver;
                Array.Clear(held, 0, held.Length);
            }
            LastTimingError = error; Judged?.Invoke(new JudgmentEvent(i, chart[i], grade, error));
        }
    }
}
