using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using UnityEngine;

public sealed class AzureSpeechManager
{
    private readonly string _key;
    private readonly string _endpoint;
    private readonly string _language;

    public AzureSpeechManager(string key, string endpoint, string language = "en-US")
    {
        _key = key?.Trim().Trim('"');
        _endpoint = endpoint?.Trim().Trim('"');
        _language = language;

        if (string.IsNullOrWhiteSpace(_key) || string.IsNullOrWhiteSpace(_endpoint))
            throw new ApplicationException("AzureSpeechManager: Missing SPEECH_KEY or ENDPOINT. " +
                                           "Unity usually doesn’t inherit shell environment variables. " +
                                           "Pass them manually or load from config.");
    }

    public sealed class AzureWord
    {
        public string Text;
        public double StartSec;
        public double EndSec;
    }

    public sealed class AzureResult
    {
        public string Text;
        public AzureWord[] Words;
        public ResultReason Reason;
        public string ErrorDetails;
    }

    /// <summary>
    /// Saves the AudioClip as a 16 kHz mono PCM WAV to a temp file and calls Azure with FromWavFileInput.
    /// </summary>
    public async Task<AzureResult> GetTextAsync(AudioClip clip)
    {
        if (clip == null || clip.samples == 0)
            return new AzureResult { Text = null, Words = Array.Empty<AzureWord>(), Reason = ResultReason.NoMatch };

        // 1) Prepare WAV temp file (16k mono PCM16)
        string tempDir = string.IsNullOrEmpty(Application.temporaryCachePath)
            ? Application.persistentDataPath
            : Application.temporaryCachePath;

        Directory.CreateDirectory(tempDir);
        string wavPath = Path.Combine(tempDir, $"stt_{Guid.NewGuid():N}.wav");

        try
        {
            WriteWav16kMono(clip, wavPath);

       
            var speechConfig = SpeechConfig.FromEndpoint(new Uri(_endpoint), _key);
            speechConfig.SpeechRecognitionLanguage = _language;
            speechConfig.OutputFormat = OutputFormat.Detailed;
            speechConfig.RequestWordLevelTimestamps();
            speechConfig.SetProperty(PropertyId.Speech_SegmentationSilenceTimeoutMs, "2000");


            using var audioConfig = AudioConfig.FromWavFileInput(wavPath);
            using var recognizer = new SpeechRecognizer(speechConfig, audioConfig);
            var result = await recognizer.RecognizeOnceAsync().ConfigureAwait(false);

            if (result.Reason == ResultReason.Canceled)
            {
                var cancel = CancellationDetails.FromResult(result);
                return new AzureResult
                {
                    Text = null,
                    Words = Array.Empty<AzureWord>(),
                    Reason = result.Reason,
                    ErrorDetails = cancel?.ErrorDetails
                };
            }

            var top = result.Best()?.FirstOrDefault();
            var words = top?.Words?.Select(w => new AzureWord
            {
                Text = w.Word,
                StartSec = w.Offset / 10_000_000.0,              // ticks → seconds
                EndSec = (w.Offset + w.Duration) / 10_000_000.0
            }).ToArray() ?? Array.Empty<AzureWord>();

            return new AzureResult
            {
                Text = result.Text,
                Words = words,
                Reason = result.Reason,
                ErrorDetails = null
            };
        }
        catch (Exception ex)
        {
            return new AzureResult
            {
                Text = null,
                Words = Array.Empty<AzureWord>(),
                Reason = ResultReason.Canceled,
                ErrorDetails = ex.Message
            };
        }
        finally
        {
            try { if (File.Exists(wavPath)) File.Delete(wavPath); } catch { /* ignore */ }
        }
    }

    // ----------------- WAV helpers -----------------

    private static void WriteWav16kMono(AudioClip clip, string path)
    {
        // Resample to 16k mono PCM16
        float[] mono16k = ResampleTo16kMono(clip);
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
        using var bw = new BinaryWriter(fs);

        int sampleCount = mono16k.Length;
        int byteRate = 16000 * 1 * 16 / 8;
        short blockAlign = (short)(1 * 16 / 8);
        int subchunk2Size = sampleCount * 2;
        int chunkSize = 36 + subchunk2Size;

        // RIFF header
        bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        bw.Write(chunkSize);
        bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));

        // fmt  subchunk
        bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        bw.Write(16);                 // Subchunk1Size for PCM
        bw.Write((short)1);           // AudioFormat = PCM
        bw.Write((short)1);           // NumChannels = 1
        bw.Write(16000);              // SampleRate
        bw.Write(byteRate);           // ByteRate
        bw.Write(blockAlign);         // BlockAlign
        bw.Write((short)16);          // BitsPerSample

        // data subchunk
        bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        bw.Write(subchunk2Size);

        // PCM samples
        for (int i = 0; i < sampleCount; i++)
        {
            short s = FloatToPCM16(mono16k[i]);
            bw.Write(s);
        }
    }

    private static float[] ResampleTo16kMono(AudioClip clip)
    {
        int total = clip.samples * clip.channels;
        var interleaved = new float[total];
        clip.GetData(interleaved, 0);

        int frames = clip.samples;
        var mono = new float[frames];

        if (clip.channels == 1)
        {
            Array.Copy(interleaved, mono, frames);
        }
        else
        {
            for (int i = 0; i < frames; i++)
            {
                float sum = 0f;
                for (int c = 0; c < clip.channels; c++)
                    sum += interleaved[i * clip.channels + c];
                mono[i] = sum / clip.channels;
            }
        }

        int srcRate = clip.frequency <= 0 ? 16000 : clip.frequency;
        const int dstRate = 16000;

        if (srcRate == dstRate)
        {
            return (float[])mono.Clone();
        }

        float ratio = (float)dstRate / srcRate;
        int dstFrames = Mathf.Max(1, Mathf.RoundToInt(mono.Length * ratio));
        var dst = new float[dstFrames];

        for (int i = 0; i < dstFrames; i++)
        {
            float srcPos = i / ratio;
            int i0 = Mathf.Clamp((int)Mathf.Floor(srcPos), 0, mono.Length - 1);
            int i1 = Mathf.Clamp(i0 + 1, 0, mono.Length - 1);
            float t = srcPos - i0;
            dst[i] = Mathf.Lerp(mono[i0], mono[i1], t);
        }

        return dst;
    }

    private static short FloatToPCM16(float f)
    {
        var clamped = Mathf.Clamp(f, -1f, 1f);
        return (short)Mathf.RoundToInt(clamped * short.MaxValue);
    }
}
