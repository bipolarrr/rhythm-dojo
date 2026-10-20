using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RhythmDojo.Authoring
{
    public sealed class WaveformPeaks
    {
        private const int FramesPerPeak = 64;
        private readonly List<Vector2[]> levels = new List<Vector2[]>();
        private readonly List<Vector3[]> bandLevels = new List<Vector3[]>();
        private int frequency;
        private int samples;

        public float Progress { get; private set; }
        public bool Ready { get; private set; }

        public IEnumerator Build(AudioClip clip)
        {
            Ready = false;
            levels.Clear();
            bandLevels.Clear();
            frequency = clip.frequency;
            samples = clip.samples;
            const int framesPerRead = 16384;
            var buffer = new float[Math.Min(framesPerRead, samples) * clip.channels];
            var peaks = new Vector2[(samples + FramesPerPeak - 1) / FramesPerPeak];
            var bands = new Vector3[peaks.Length];
            var low = new float[clip.channels];
            var upper = new float[clip.channels];
            float lowCoefficient = (float)(1 - Math.Exp(-2 * Math.PI * 250 / frequency));
            float upperCoefficient = (float)(1 - Math.Exp(-2 * Math.PI * 4000 / frequency));
            for (int offset = 0; offset < samples; offset += framesPerRead)
            {
                if (!clip.GetData(buffer, offset))
                {
                    throw new InvalidOperationException("음원 샘플을 읽을 수 없습니다. PCM 디코딩을 확인하세요.");
                }

                int count = Math.Min(framesPerRead, samples - offset);
                for (int frame = 0; frame < count; frame += FramesPerPeak)
                {
                    float min = 0;
                    float max = 0;
                    Vector3 energy = Vector3.zero;
                    int end = Math.Min(count, frame + FramesPerPeak);
                    for (int i = frame * clip.channels; i < end * clip.channels; i++)
                    {
                        min = Math.Min(min, buffer[i]);
                        max = Math.Max(max, buffer[i]);
                        int channel = i % clip.channels;
                        float sample = buffer[i];
                        low[channel] += lowCoefficient * (sample - low[channel]);
                        upper[channel] += upperCoefficient * (sample - upper[channel]);
                        float middle = upper[channel] - low[channel];
                        float high = sample - upper[channel];
                        // Keep channel energy separate so opposite-phase stereo cannot cancel.
                        energy.x += low[channel] * low[channel];
                        energy.y += middle * middle;
                        energy.z += high * high;
                    }

                    peaks[(offset + frame) / FramesPerPeak] = new Vector2(min, max);
                    bands[(offset + frame) / FramesPerPeak] = energy;
                }

                Progress = (float)(offset + count) / samples;
                yield return null;
            }

            levels.Add(peaks);
            bandLevels.Add(bands);
            while (peaks.Length > 1)
            {
                var next = new Vector2[(peaks.Length + 1) / 2];
                var nextBands = new Vector3[next.Length];
                for (int i = 0; i < next.Length; i++)
                {
                    int first = i * 2;
                    int last = Math.Min(first + 1, peaks.Length - 1);
                    next[i] = new Vector2(Math.Min(peaks[first].x, peaks[last].x),
                        Math.Max(peaks[first].y, peaks[last].y));
                    nextBands[i] = bands[first];
                    if (last != first)
                    {
                        nextBands[i] += bands[last];
                    }
                }

                levels.Add(next);
                peaks = next;
                bandLevels.Add(nextBands);
                bands = nextBands;
                yield return null;
            }

            Ready = true;
        }

        public Color ColorRange(double startSeconds, double endSeconds)
        {
            var silence = new Color(0.22f, 0.25f, 0.3f);
            if (!Ready || endSeconds <= startSeconds)
            {
                return silence;
            }

            double duration = (double)samples / frequency;
            if (endSeconds <= 0 || startSeconds >= duration)
            {
                return silence;
            }

            int start = (int)Math.Floor(Math.Max(0, startSeconds) * frequency / FramesPerPeak);
            int end = Math.Min(bandLevels[0].Length,
                (int)Math.Ceiling(Math.Min(duration, endSeconds) * frequency / FramesPerPeak));
            int level = 0;
            int width = 1;
            while (level + 1 < bandLevels.Count && width * 2 <= Math.Max(1, (end - start) / 2))
            {
                level++;
                width *= 2;
            }

            Vector3 energy = Vector3.zero;
            var bands = bandLevels[level];
            for (int i = start / width; i < Math.Min(bands.Length, (end + width - 1) / width); i++)
            {
                energy += bands[i];
            }

            float strongest = Math.Max(energy.x, Math.Max(energy.y, energy.z));
            if (strongest <= 1e-12f)
            {
                return silence;
            }

            // Normalize hue independently from the original min/max waveform amplitude.
            return new Color(
                0.12f + 0.88f * (float)Math.Sqrt(energy.x / strongest),
                0.12f + 0.88f * (float)Math.Sqrt(energy.y / strongest),
                0.12f + 0.88f * (float)Math.Sqrt(energy.z / strongest));
        }

        public Vector2 Range(double startSeconds, double endSeconds)
        {
            if (!Ready || endSeconds <= 0 || startSeconds >= (double)samples / frequency)
            {
                return Vector2.zero;
            }

            int start = Math.Max(0, (int)Math.Floor(startSeconds * frequency / FramesPerPeak));
            int end = Math.Min(levels[0].Length, (int)Math.Ceiling(endSeconds * frequency / FramesPerPeak));
            int level = 0;
            int width = 1;
            while (level + 1 < levels.Count && width * 2 <= Math.Max(1, (end - start) / 2))
            {
                level++;
                width *= 2;
            }

            // Include edge bins, preserving short transients at every resolution.
            var peaks = levels[level];
            Vector2 result = Vector2.zero;
            for (int i = start / width; i < Math.Min(peaks.Length, (end + width - 1) / width); i++)
            {
                result.x = Math.Min(result.x, peaks[i].x);
                result.y = Math.Max(result.y, peaks[i].y);
            }

            return result;
        }
    }
}
