using System;
using System.Collections.Generic;
using System.IO;

namespace Coika.Tools
{
    /// <summary>
    /// Tiny synthesizer that writes the placeholder sounds as 16-bit WAV files. Everything is computed from formulas
    /// and a fixed noise seed, so the files are self-made, CC0 and identical on every run.
    /// </summary>
    public static class ToneSynth
    {
        private const double TWO_PI = Math.PI * 2.0;

        /// <summary>
        /// Builds a sine tone that glides from one frequency to another with a fast attack and an exponential decay.
        /// </summary>
        /// <param name="rate">Sample rate in Hz.</param>
        /// <param name="seconds">Length of the tone.</param>
        /// <param name="startHz">Frequency at the start.</param>
        /// <param name="endHz">Frequency at the end.</param>
        /// <param name="decay">Decay speed; higher fades faster.</param>
        /// <param name="harmonic">Level from 0 to 1 of the octave above, for a brighter tone.</param>
        /// <returns>Mono samples from -1 to 1.</returns>
        public static float[] Glide(int rate, double seconds, double startHz, double endHz, double decay, double harmonic = 0.0)
        {
            var count = (int)(seconds * rate);
            var samples = new float[count];
            double phase = 0.0;
            for (int i = 0; i < count; i++)
            {
                var t = (double)i / count;
                var hz = startHz + (endHz - startHz) * t;
                phase += TWO_PI * hz / rate;
                var tone = Math.Sin(phase) + harmonic * Math.Sin(phase * 2.0);
                samples[i] = (float)(tone * Envelope(i, count, rate, decay) * 0.5);
            }

            return samples;
        }

        /// <summary>
        /// Builds a soft thump: a low sine plus a burst of noise that dies quickly.
        /// </summary>
        /// <param name="rate">Sample rate in Hz.</param>
        /// <param name="seconds">Length of the sound.</param>
        /// <param name="hz">Pitch of the body.</param>
        /// <param name="seed">Seed of the noise, so each variant differs.</param>
        /// <returns>Mono samples from -1 to 1.</returns>
        public static float[] Thump(int rate, double seconds, double hz, int seed)
        {
            var body = Glide(rate, seconds, hz * 1.4, hz, 22.0);
            var state = (uint)seed * 2654435761u + 1u;
            for (int i = 0; i < body.Length; i++)
            {
                state = state * 1664525u + 1013904223u;
                var noise = ((state >> 8) / (double)(1 << 24)) * 2.0 - 1.0;
                body[i] += (float)(noise * Math.Exp(-120.0 * i / rate) * 0.25);
            }

            return body;
        }

        /// <summary>
        /// Joins sounds one after another.
        /// </summary>
        /// <param name="parts">The sounds, in order.</param>
        /// <returns>The joined samples.</returns>
        public static float[] Concat(params float[][] parts)
        {
            var all = new List<float>();
            foreach (var part in parts)
            {
                all.AddRange(part);
            }

            return all.ToArray();
        }

        /// <summary>
        /// Builds a stereo loop of a four-chord arpeggio. The loop length is a whole number of bars, so it repeats
        /// without a click.
        /// </summary>
        /// <param name="rate">Sample rate in Hz.</param>
        /// <param name="bpm">Tempo in beats per minute.</param>
        /// <param name="rootsHz">Root frequency of each of the four chords.</param>
        /// <param name="notesPerBeat">How many arpeggio notes fit in a beat.</param>
        /// <returns>Interleaved stereo samples from -1 to 1.</returns>
        public static float[] ArpeggioLoop(int rate, double bpm, double[] rootsHz, int notesPerBeat)
        {
            const int BEATS_PER_CHORD = 3;
            var noteSeconds = 60.0 / bpm / notesPerBeat;
            var notesPerChord = BEATS_PER_CHORD * notesPerBeat;
            var noteSamples = (int)(noteSeconds * rate);
            var frames = noteSamples * notesPerChord * rootsHz.Length;
            var stereo = new float[frames * 2];
            double[] intervals = { 1.0, 1.25, 1.5, 2.0 }; // Major triad and the octave

            for (int chord = 0; chord < rootsHz.Length; chord++)
            {
                for (int note = 0; note < notesPerChord; note++)
                {
                    var hz = rootsHz[chord] * intervals[note % intervals.Length];
                    var start = (chord * notesPerChord + note) * noteSamples;
                    var pan = 0.5 + 0.3 * Math.Sin(note);
                    for (int i = 0; i < noteSamples; i++)
                    {
                        var envelope = Math.Min(1.0, i / 200.0) * Math.Exp(-3.0 * i / noteSamples);
                        var value = Math.Sin(TWO_PI * hz * i / rate) * envelope * 0.25;
                        stereo[(start + i) * 2] += (float)(value * (1.0 - pan));
                        stereo[(start + i) * 2 + 1] += (float)(value * pan);
                    }
                }
            }

            return stereo;
        }

        /// <summary>
        /// Writes samples as a 16-bit PCM WAV file.
        /// </summary>
        /// <param name="path">File to write; an existing file is replaced.</param>
        /// <param name="samples">Samples from -1 to 1, interleaved when there is more than one channel.</param>
        /// <param name="rate">Sample rate in Hz.</param>
        /// <param name="channels">Number of channels.</param>
        public static void WriteWav(string path, float[] samples, int rate, int channels)
        {
            using var writer = new BinaryWriter(File.Create(path));
            var dataBytes = samples.Length * 2;
            writer.Write(new[] { 'R', 'I', 'F', 'F' });
            writer.Write(36 + dataBytes);
            writer.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)channels);
            writer.Write(rate);
            writer.Write(rate * channels * 2);
            writer.Write((short)(channels * 2));
            writer.Write((short)16);
            writer.Write(new[] { 'd', 'a', 't', 'a' });
            writer.Write(dataBytes);
            foreach (var sample in samples)
            {
                writer.Write((short)(Math.Max(-1.0, Math.Min(1.0, sample)) * short.MaxValue));
            }
        }

        /// <summary>
        /// Attack of about 5 ms followed by an exponential decay.
        /// </summary>
        /// <param name="index">Sample index.</param>
        /// <param name="count">Number of samples.</param>
        /// <param name="rate">Sample rate in Hz.</param>
        /// <param name="decay">Decay speed.</param>
        /// <returns>Gain from 0 to 1.</returns>
        private static double Envelope(int index, int count, int rate, double decay)
        {
            var attack = Math.Min(1.0, index / (0.005 * rate));
            var tail = Math.Min(1.0, (count - index) / (0.01 * rate)); // No click at the end
            return attack * tail * Math.Exp(-decay * index / count);
        }
    }
}
