// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║                        8 BIT LAB ENGINEERING                               ║
// ║                     WORKFLOW - TEAM TIME TRACKER                            ║
// ║                                                                            ║
// ║  FILE:        SoundManager.cs                                              ║
// ║  PURPOSE:     PROCEDURALLY GENERATED NOTIFICATION SOUNDS                   ║
// ║  AUTHOR:      8BitLab Engineering (info@8bitlab.de)                         ║
// ║  LICENSE:     OPEN SOURCE                                                  ║
// ║  GitHub:      https://github.com/8BitLabEngineering                        ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

using System;
using System.IO;
using System.Media;
using System.Threading.Tasks;

namespace Work_Time_Counter
{
    /// <summary>
    /// Generates and plays short notification sounds using in-memory WAV data.
    /// No external sound files needed — all tones are synthesized at runtime.
    /// </summary>
    public static class SoundManager
    {
        private static bool _enabled = true;
        public static bool Enabled
        {
            get => _enabled;
            set => _enabled = value;
        }

        // ════════════════════════════════════════════════════════
        //  PUBLIC SOUND METHODS
        // ════════════════════════════════════════════════════════

        /// <summary>Chat message sent by current user — short soft click</summary>
        public static void PlayChatSend()
        {
            // [SoundManager] Play chat send notification sound (880 Hz, 60ms)
            if (!_enabled)
            {
//                 DebugLogger.Log("[SoundManager] PlayChatSend - sounds disabled, skipped");
                return;
            }
//             DebugLogger.Log("[SoundManager] PlayChatSend - generating tone");
            PlayToneAsync(880, 60, 0.25);
        }

        /// <summary>New chat message received from another user — two-tone chime</summary>
        public static void PlayChatReceive()
        {
            // [SoundManager] Play chat received notification (two-tone chime)
            if (!_enabled)
            {
//                 DebugLogger.Log("[SoundManager] PlayChatReceive - sounds disabled, skipped");
                return;
            }
//             DebugLogger.Log("[SoundManager] PlayChatReceive - generating multi-tone");
            PlayMultiToneAsync(new[] { (660, 80, 0.3), (880, 120, 0.3) });
        }

        /// <summary>Checkbox toggled (sticker done/undone) — tiny tick</summary>
        public static void PlayCheckbox()
        {
            // [SoundManager] Play checkbox toggle sound (1200 Hz, 40ms)
            if (!_enabled)
            {
//                 DebugLogger.Log("[SoundManager] PlayCheckbox - sounds disabled, skipped");
                return;
            }
//             DebugLogger.Log("[SoundManager] PlayCheckbox - generating tone");
            PlayToneAsync(1200, 40, 0.2);
        }

        /// <summary>New sticker added to board — upward sweep</summary>
        public static void PlayStickerAdded()
        {
            // [SoundManager] Play sticker added notification (upward sweep)
            if (!_enabled)
            {
//                 DebugLogger.Log("[SoundManager] PlayStickerAdded - sounds disabled, skipped");
                return;
            }
//             DebugLogger.Log("[SoundManager] PlayStickerAdded - generating multi-tone");
            PlayMultiToneAsync(new[] { (440, 70, 0.25), (660, 70, 0.25), (880, 90, 0.3) });
        }

        /// <summary>User came online — pleasant rising two-note</summary>
        public static void PlayUserOnline()
        {
            // [SoundManager] Play user online notification (rising two-note)
            if (!_enabled)
            {
//                 DebugLogger.Log("[SoundManager] PlayUserOnline - sounds disabled, skipped");
                return;
            }
//             DebugLogger.Log("[SoundManager] PlayUserOnline - generating multi-tone");
            PlayMultiToneAsync(new[] { (523, 100, 0.3), (784, 150, 0.3) });
        }

        /// <summary>User went offline — descending two-note</summary>
        public static void PlayUserOffline()
        {
            // [SoundManager] Play user offline notification (descending two-note)
            if (!_enabled)
            {
//                 DebugLogger.Log("[SoundManager] PlayUserOffline - sounds disabled, skipped");
                return;
            }
//             DebugLogger.Log("[SoundManager] PlayUserOffline - generating multi-tone");
            PlayMultiToneAsync(new[] { (784, 100, 0.2), (523, 150, 0.2) });
        }

        /// <summary>PING / Ring alert — attention-grabbing triple beep</summary>
        public static void PlayPingAlert()
        {
            // [SoundManager] Play ping alert sound (attention-grabbing triple beep)
            if (!_enabled)
            {
//                 DebugLogger.Log("[SoundManager] PlayPingAlert - sounds disabled, skipped");
                return;
            }
//             DebugLogger.Log("[SoundManager] PlayPingAlert - generating multi-tone");
            PlayMultiToneAsync(new[] {
                (880, 120, 0.5), (0, 60, 0.0),
                (880, 120, 0.5), (0, 60, 0.0),
                (1174, 200, 0.5)
            });
        }

        /// <summary>Ping sent confirmation — single soft boop</summary>
        public static void PlayPingSent()
        {
            // [SoundManager] Play ping sent confirmation sound (660 Hz, 100ms)
            if (!_enabled)
            {
//                 DebugLogger.Log("[SoundManager] PlayPingSent - sounds disabled, skipped");
                return;
            }
//             DebugLogger.Log("[SoundManager] PlayPingSent - generating tone");
            PlayToneAsync(660, 100, 0.25);
        }

        // ════════════════════════════════════════════════════════
        //  WAV SYNTHESIS ENGINE
        // ════════════════════════════════════════════════════════

        private static void PlayToneAsync(int frequencyHz, int durationMs, double volume)
        {
            // [SoundManager] Play single tone asynchronously (non-blocking)
            Task.Run(() =>
            {
                try
                {
//                     DebugLogger.Log($"[SoundManager] PlayToneAsync: {frequencyHz}Hz, {durationMs}ms, vol={volume}");
                    byte[] wav = GenerateToneWav(frequencyHz, durationMs, volume);
                    using (var ms = new MemoryStream(wav))
                    using (var player = new SoundPlayer(ms))
                    {
                        player.PlaySync();
//                         DebugLogger.Log("[SoundManager] Tone playback completed");
                    }
                }
                catch (Exception ex)
                {
                    DebugLogger.Log($"[SoundManager] ERROR playing tone: {ex.Message}");
                }
            });
        }

        private static void PlayMultiToneAsync((int freq, int ms, double vol)[] tones)
        {
            // [SoundManager] Play multi-tone sequence asynchronously (non-blocking)
            Task.Run(() =>
            {
                try
                {
//                     DebugLogger.Log($"[SoundManager] PlayMultiToneAsync: {tones.Length} tones");
                    byte[] wav = GenerateMultiToneWav(tones);
                    using (var ms = new MemoryStream(wav))
                    using (var player = new SoundPlayer(ms))
                    {
                        player.PlaySync();
//                         DebugLogger.Log("[SoundManager] Multi-tone playback completed");
                    }
                }
                catch (Exception ex)
                {
                    DebugLogger.Log($"[SoundManager] ERROR playing multi-tone: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Generates a PCM 16-bit mono WAV file in memory for a single tone.
        /// </summary>
        private static byte[] GenerateToneWav(int frequencyHz, int durationMs, double volume)
        {
            // [SoundManager] Generate WAV file data for single sine wave tone
//             DebugLogger.Log($"[SoundManager] GenerateToneWav: freq={frequencyHz}Hz, dur={durationMs}ms");
            int sampleRate = 22050;
            int numSamples = (int)(sampleRate * durationMs / 1000.0);
            int dataSize = numSamples * 2; // 16-bit = 2 bytes per sample

            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                // WAV header
                bw.Write(new char[] { 'R', 'I', 'F', 'F' });
                bw.Write(36 + dataSize);
                bw.Write(new char[] { 'W', 'A', 'V', 'E' });
                bw.Write(new char[] { 'f', 'm', 't', ' ' });
                bw.Write(16);            // chunk size
                bw.Write((short)1);      // PCM
                bw.Write((short)1);      // mono
                bw.Write(sampleRate);
                bw.Write(sampleRate * 2); // byte rate
                bw.Write((short)2);      // block align
                bw.Write((short)16);     // bits per sample
                bw.Write(new char[] { 'd', 'a', 't', 'a' });
                bw.Write(dataSize);

                // Generate samples with fade-in/out envelope
                for (int i = 0; i < numSamples; i++)
                {
                    double t = (double)i / sampleRate;
                    double envelope = GetEnvelope(i, numSamples);
                    double sample;

                    if (frequencyHz == 0)
                        sample = 0; // silence
                    else
                        sample = Math.Sin(2 * Math.PI * frequencyHz * t) * volume * envelope;

                    short pcm = (short)(sample * short.MaxValue);
                    bw.Write(pcm);
                }

                return ms.ToArray();
            }
        }

        /// <summary>
        /// Generates a multi-tone WAV by concatenating tones sequentially.
        /// </summary>
        private static byte[] GenerateMultiToneWav((int freq, int ms, double vol)[] tones)
        {
            // [SoundManager] Generate WAV file data for multi-tone sequence
//             DebugLogger.Log($"[SoundManager] GenerateMultiToneWav: {tones.Length} sequential tones");
            int sampleRate = 22050;
            int totalSamples = 0;
            foreach (var t in tones)
                totalSamples += (int)(sampleRate * t.ms / 1000.0);

            int dataSize = totalSamples * 2;

            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                // WAV header
                bw.Write(new char[] { 'R', 'I', 'F', 'F' });
                bw.Write(36 + dataSize);
                bw.Write(new char[] { 'W', 'A', 'V', 'E' });
                bw.Write(new char[] { 'f', 'm', 't', ' ' });
                bw.Write(16);
                bw.Write((short)1);
                bw.Write((short)1);
                bw.Write(sampleRate);
                bw.Write(sampleRate * 2);
                bw.Write((short)2);
                bw.Write((short)16);
                bw.Write(new char[] { 'd', 'a', 't', 'a' });
                bw.Write(dataSize);

                // Write each tone segment
                foreach (var tone in tones)
                {
                    int numSamples = (int)(sampleRate * tone.ms / 1000.0);
                    for (int i = 0; i < numSamples; i++)
                    {
                        double t = (double)i / sampleRate;
                        double envelope = GetEnvelope(i, numSamples);
                        double sample;

                        if (tone.freq == 0)
                            sample = 0;
                        else
                            sample = Math.Sin(2 * Math.PI * tone.freq * t) * tone.vol * envelope;

                        short pcm = (short)(sample * short.MaxValue);
                        bw.Write(pcm);
                    }
                }

                return ms.ToArray();
            }
        }

        /// <summary>
        /// Smooth fade-in (5%) and fade-out (15%) envelope to avoid clicks.
        /// </summary>
        private static double GetEnvelope(int sampleIndex, int totalSamples)
        {
            double pos = (double)sampleIndex / totalSamples;
            double fadeIn = 0.05;
            double fadeOut = 0.15;

            if (pos < fadeIn)
                return pos / fadeIn;
            else if (pos > (1.0 - fadeOut))
                return (1.0 - pos) / fadeOut;
            else
                return 1.0;
        }
    }
}
