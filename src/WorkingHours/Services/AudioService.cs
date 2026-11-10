using System.IO;
using System.Media;

namespace WorkingHours.Services;

/// <summary>
/// Generates its own alert tones as in-memory WAV files, so the app needs no
/// external audio assets and respects the configured volume.
/// </summary>
public sealed class AudioService : IDisposable
{
    private const int SampleRate = 44100;
    private SoundPlayer? _lastPlayer;

    public double Volume { get; set; } = 1.0;
    public bool IsMuted { get; set; }

    public void PlayWorkComplete() => Play(new[]
    {
        new Note(784.0, 0.11), new Note(1046.5, 0.11), new Note(1318.5, 0.28)
    });

    public void PlayBreakComplete() => Play(new[]
    {
        new Note(1318.5, 0.11), new Note(1046.5, 0.11), new Note(784.0, 0.24)
    });

    public void PlayTick() => Play(new[]
    {
        new Note(1500.0, 0.025)
    }, amplitude: 0.16);

    public void PlayTest() => PlayWorkComplete();

    private void Play(IReadOnlyList<Note> notes, double amplitude = 0.5)
    {
        if (IsMuted || Volume <= 0.001)
            return;

        try
        {
            var samples = Render(notes, amplitude * Volume);
            var player = new SoundPlayer(new MemoryStream(samples));
            var previous = _lastPlayer;
            _lastPlayer = player;
            player.Play();
            previous?.Dispose();
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException)
        {
            // Audio is a nice-to-have; never let it break the timer.
        }
    }

    private static byte[] Render(IReadOnlyList<Note> notes, double masterAmplitude)
    {
        var totalSeconds = notes.Sum(n => n.DurationSeconds) + 0.18;
        var sampleCount = (int)(totalSeconds * SampleRate);
        var samples = new short[sampleCount];
        var cursor = 0;

        foreach (var note in notes)
        {
            var noteSamples = (int)(note.DurationSeconds * SampleRate);
            var releaseSamples = Math.Min(noteSamples / 2, (int)(0.08 * SampleRate));

            for (var i = 0; i < noteSamples && cursor + i < samples.Length; i++)
            {
                var t = (double)i / SampleRate;
                var progress = (double)i / noteSamples;
                var attack = Math.Min(1.0, i / (0.008 * SampleRate));
                var release = releaseSamples > 0 && i > noteSamples - releaseSamples
                    ? (double)(noteSamples - i) / releaseSamples
                    : 1.0;
                var envelope = attack * release * Math.Pow(1.0 - progress, 1.6);

                var wave = Math.Sin(2 * Math.PI * note.Frequency * t) +
                           0.28 * Math.Sin(4 * Math.PI * note.Frequency * t) +
                           0.12 * Math.Sin(6 * Math.PI * note.Frequency * t);

                var value = wave / 1.4 * envelope * masterAmplitude;
                samples[cursor + i] = (short)Math.Clamp(value * short.MaxValue, short.MinValue, short.MaxValue);
            }

            cursor += noteSamples;
        }

        using var stream = new MemoryStream(44 + samples.Length * 2);
        using var writer = new BinaryWriter(stream);
        var dataLength = samples.Length * 2;

        writer.Write("RIFF"u8.ToArray());
        writer.Write(36 + dataLength);
        writer.Write("WAVE"u8.ToArray());
        writer.Write("fmt "u8.ToArray());
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(SampleRate);
        writer.Write(SampleRate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write("data"u8.ToArray());
        writer.Write(dataLength);

        foreach (var sample in samples)
            writer.Write(sample);

        writer.Flush();
        return stream.ToArray();
    }

    public void Dispose()
    {
        _lastPlayer?.Dispose();
        _lastPlayer = null;
    }

    private readonly record struct Note(double Frequency, double DurationSeconds);
}
