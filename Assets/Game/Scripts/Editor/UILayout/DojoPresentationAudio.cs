using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace RhythmDojo.EditorTools
{
    // Short presentation foley is baked once; runtime playback uses the authored effects source.
    public static class DojoPresentationAudio
    {
        public static AudioClip Load(bool wind)
        {
            string name = "TrainingFootstep";
            float duration = .13f;
            if (wind)
            {
                name = "SummitGust";
                duration = 1.1f;
            }
            string path = "Assets/Game/Audio/" + name + ".wav";
            if (!File.Exists(path))
            {
                const int sampleRate = 22050;
                int count = Mathf.RoundToInt(sampleRate * duration);
                using (var writer = new BinaryWriter(File.Create(path)))
                {
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                    writer.Write(36 + count * 2);
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                    writer.Write(16);
                    writer.Write((short)1);
                    writer.Write((short)1);
                    writer.Write(sampleRate);
                    writer.Write(sampleRate * 2);
                    writer.Write((short)2);
                    writer.Write((short)16);
                    writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                    writer.Write(count * 2);
                    var random = new System.Random(73);
                    float filtered = 0f;
                    for (int i = 0; i < count; i++)
                    {
                        float t = (float)i / sampleRate;
                        float noise = (float)random.NextDouble() * 2 - 1;
                        filtered = Mathf.Lerp(filtered, noise, .09f);
                        float sample = (filtered + Mathf.Sin(t * 2 * Mathf.PI * 145) * .24f)
                            * Mathf.Exp(-t * 48) * Mathf.Min(1, t * 1400);
                        if (wind)
                        {
                            sample = filtered * Mathf.Pow(Mathf.Sin(t / duration * Mathf.PI), 2) * .85f;
                        }
                        writer.Write((short)(Mathf.Clamp(sample, -1, 1) * short.MaxValue));
                    }
                }
                AssetDatabase.ImportAsset(path);
            }
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
    }
}
