using System;
using RhythmDojo.Gameplay;

namespace RhythmDojo.Application
{
    public enum ScrollMode { Constant, Bpm }
    public interface IScrollTimeline { double DistanceBetween(double fromSongTime, double toSongTime); }
    public sealed class ConstantScrollTimeline : IScrollTimeline
    {
        private readonly double speed;
        public ConstantScrollTimeline(double baseSpeed, double multiplier)
        { speed = ScrollValidation.Speed(baseSpeed, multiplier); }
        public double DistanceBetween(double fromSongTime, double toSongTime)
        {
            ScrollValidation.Time(fromSongTime); ScrollValidation.Time(toSongTime);
            return (toSongTime - fromSongTime) * speed;
        }
    }
    public sealed class BpmScrollTimeline : IScrollTimeline
    {
        private readonly TempoMap tempo;
        private readonly double[] distances, speeds;
        public BpmScrollTimeline(TempoMap tempo, double baseSpeed, double referenceBpm, double multiplier)
        {
            this.tempo = tempo ?? throw new ArgumentNullException(nameof(tempo));
            double speed = ScrollValidation.Speed(baseSpeed, multiplier);
            if (!double.IsFinite(referenceBpm) || referenceBpm <= 0) throw new ArgumentOutOfRangeException(nameof(referenceBpm));
            distances = new double[tempo.Count]; speeds = new double[tempo.Count];
            for (int i = 0; i < tempo.Count; i++)
            {
                speeds[i] = speed * tempo[i].Bpm / referenceBpm;
                if (!double.IsFinite(speeds[i]) || speeds[i] <= 0) throw new ArgumentException("Scroll speed overflow.");
                if (i > 0) distances[i] = distances[i - 1] +
                    (tempo[i].StartTimeSeconds - tempo[i - 1].StartTimeSeconds) * speeds[i - 1];
                if (!double.IsFinite(distances[i])) throw new ArgumentException("Scroll distance overflow.");
            }
        }
        private double PositionAt(double time)
        {
            int i = tempo.FindSegment(time);
            return distances[i] + (time - tempo[i].StartTimeSeconds) * speeds[i];
        }
        public double DistanceBetween(double fromSongTime, double toSongTime) => PositionAt(toSongTime) - PositionAt(fromSongTime);
    }
    internal static class ScrollValidation
    {
        public static double Speed(double speed, double multiplier)
        {
            if (!double.IsFinite(speed) || speed <= 0 || !double.IsFinite(multiplier) || multiplier <= 0 ||
                !double.IsFinite(speed * multiplier)) throw new ArgumentOutOfRangeException(nameof(speed));
            return speed * multiplier;
        }
        public static void Time(double time)
        { if (!double.IsFinite(time)) throw new ArgumentOutOfRangeException(nameof(time)); }
    }
}
