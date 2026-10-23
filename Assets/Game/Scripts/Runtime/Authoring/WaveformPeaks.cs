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
        private AudioClip audioClip;
        private float[] detailBuffer;
        private int detailOffset = -1;

        public float Progress { get; private set; }
        public bool Ready { get; private set; }
        public int Frequency => frequency;
        public int SampleCount => samples;
        public int Channels { get; private set; }
        public int Revision { get; private set; }

        public IEnumerator Build(AudioClip clip)
        {
            Ready = false;
            Revision++;
            Progress = 0;
            levels.Clear();
            bandLevels.Clear();
            frequency = clip.frequency;
            samples = clip.samples;
            audioClip = clip;
            Channels = clip.channels;
            detailOffset = -1;
            detailBuffer = new float[Math.Min(16384, samples) * clip.channels];
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
            Vector3 energy = Vector3.zero;
            while (start < end)
            {
                int level = RangeLevel(start, end, out int width);
                energy += bandLevels[level][start / width];
                start += width;
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
            if (!Ready || endSeconds <= startSeconds)
            {
                return Vector2.zero;
            }

            if (endSeconds <= 0 || startSeconds >= (double)samples / frequency)
            {
                return Vector2.zero;
            }

            int firstFrame = FrameAtOrAfter(startSeconds);
            int lastFrame = FrameAtOrAfter(endSeconds);
            int start = (firstFrame + FramesPerPeak - 1) / FramesPerPeak;
            int end = lastFrame / FramesPerPeak;
            // Partition into aligned blocks wholly inside the requested base-bin range.
            // A coarse block crossing an edge would move a nearby transient into this pixel.
            Vector2 result = Vector2.zero;
            int leadingEnd = Math.Min(lastFrame, start * FramesPerPeak);
            IncludeFrames(firstFrame, leadingEnd, ref result);
            IncludeFrames(Math.Max(leadingEnd, end * FramesPerPeak), lastFrame, ref result);

            while (start < end)
            {
                int level = RangeLevel(start, end, out int width);
                Vector2 peak = levels[level][start / width];
                result.x = Math.Min(result.x, peak.x);
                result.y = Math.Max(result.y, peak.y);
                start += width;
            }

            return result;
        }

        private void IncludeFrames(int start, int end, ref Vector2 result)
        {
            int channels = audioClip.channels;
            for (int frame = start; frame < end; frame++)
            {
                EnsureFrame(frame);
                int offset = (frame - detailOffset) * channels;
                for (int channel = 0; channel < channels; channel++)
                {
                    float sample = detailBuffer[offset + channel];
                    result.x = Math.Min(result.x, sample);
                    result.y = Math.Max(result.y, sample);
                }
            }
        }

        // Both display modes use half-open time intervals [start, end) on n / frequency.
        public int FrameAtOrAfter(double time)
        {
            double frame = Math.Max(0, Math.Min(samples, time * frequency));
            double nearest = Math.Round(frame);
            if (Math.Abs(frame - nearest) < 1e-7)
            {
                frame = nearest;
            }

            return (int)Math.Ceiling(frame);
        }

        public float Sample(int frame, int channel)
        {
            if (!Ready)
            {
                throw new InvalidOperationException("파형 샘플이 아직 준비되지 않았습니다.");
            }

            if (frame < 0 || frame >= samples)
            {
                throw new ArgumentOutOfRangeException(nameof(frame));
            }

            if (channel < 0 || channel >= Channels)
            {
                throw new ArgumentOutOfRangeException(nameof(channel));
            }

            EnsureFrame(frame);
            return detailBuffer[(frame - detailOffset) * Channels + channel];
        }

        private void EnsureFrame(int frame)
        {
            int capacity = detailBuffer.Length / Channels;
            bool cached = detailOffset >= 0 && frame >= detailOffset;
            if (cached && frame < detailOffset + capacity)
            {
                return;
            }

            detailOffset = frame / capacity * capacity;
            if (!audioClip.GetData(detailBuffer, detailOffset))
            {
                detailOffset = -1;
                throw new InvalidOperationException("파형 상세 샘플을 읽을 수 없습니다.");
            }
        }

        private int RangeLevel(int start, int end, out int width)
        {
            int level = 0;
            width = 1;
            while (level + 1 < levels.Count)
            {
                int next = width * 2;
                if (start % next != 0 || next > end - start)
                {
                    break;
                }

                width = next;
                level++;
            }

            return level;
        }
    }
}
