using System;
using System.IO;
using System.Text;

namespace RhythmDojo.Tests
{
    // A generated PCM file exercises the external decoder without a personal audio installation.
    public sealed class TemporaryWaveFile : IDisposable
    {
        public string Path { get; }

        public TemporaryWaveFile(double durationSeconds, double firstAttackSeconds)
        {
            const int frequency = 44100;
            const int channels = 1;
            const int bytesPerSample = 2;
            int sampleCount = (int)Math.Round(durationSeconds * frequency);
            int firstAttack = (int)Math.Round(firstAttackSeconds * frequency);
            int dataLength = sampleCount * channels * bytesPerSample;
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "rhythm-dojo-verification-" + Guid.NewGuid().ToString("N") + ".wav");

            try
            {
                using var stream = File.Create(Path);
                using var writer = new BinaryWriter(stream, Encoding.ASCII);
                writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + dataLength);
                writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)channels);
                writer.Write(frequency);
                writer.Write(frequency * channels * bytesPerSample);
                writer.Write((short)(channels * bytesPerSample));
                writer.Write((short)(bytesPerSample * 8));
                writer.Write(Encoding.ASCII.GetBytes("data"));
                writer.Write(dataLength);

                for (int i = 0; i < sampleCount; i++)
                {
                    short sample = 0;
                    if (i >= firstAttack)
                    {
                        double phase = 2 * Math.PI * 440 * (i - firstAttack) / frequency;
                        sample = (short)Math.Round(short.MaxValue * .15 * Math.Cos(phase));
                    }
                    writer.Write(sample);
                }
            }
            catch
            {
                File.Delete(Path);
                throw;
            }
        }

        public void Dispose()
        {
            File.Delete(Path);
        }
    }
}
