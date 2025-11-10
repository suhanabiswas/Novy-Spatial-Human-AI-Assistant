using System;
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

    public async Task<AzureResult> GetTextAsync(AudioClip clip)
    {
        if (clip == null || clip.samples == 0)
            return new AzureResult { Text = null, Words = Array.Empty<AzureWord>(), Reason = ResultReason.NoMatch };

        var pcm16 = ClipToPCM16kMono(clip);
        var format = AudioStreamFormat.GetWaveFormatPCM(16000, 16, 1);

        using var pushStream = AudioInputStream.CreatePushStream(format);
        using var audioConfig = AudioConfig.FromStreamInput(pushStream);

        pushStream.Write(pcm16);
        pushStream.Close();

        // Endpoint-based config
        var speechConfig = SpeechConfig.FromEndpoint(new Uri(_endpoint), _key);
        speechConfig.SpeechRecognitionLanguage = _language;
        speechConfig.OutputFormat = OutputFormat.Detailed;
        speechConfig.RequestWordLevelTimestamps();

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
            StartSec = w.Offset / 10_000_000.0,
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

    // --- Helper: AudioClip → 16 kHz mono PCM16 LE ---
    private static byte[] ClipToPCM16kMono(AudioClip clip)
    {
        int totalSamples = clip.samples * clip.channels;
        var interleaved = new float[totalSamples];
        clip.GetData(interleaved, 0);

        int frames = clip.samples;
        var mono = new float[frames];
        if (clip.channels == 1)
            Array.Copy(interleaved, mono, frames);
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

        int srcRate = clip.frequency;
        const int dstRate = 16000;
        if (srcRate <= 0) srcRate = dstRate;
        float resampleRatio = (float)dstRate / srcRate;
        int dstFrames = Mathf.Max(1, Mathf.RoundToInt(mono.Length * resampleRatio));
        var mono16k = new float[dstFrames];

        if (Mathf.Approximately(resampleRatio, 1f))
            Array.Copy(mono, mono16k, Math.Min(dstFrames, mono.Length));
        else
        {
            for (int i = 0; i < dstFrames; i++)
            {
                float srcPos = i / resampleRatio;
                int i0 = Mathf.Clamp((int)Mathf.Floor(srcPos), 0, mono.Length - 1);
                int i1 = Mathf.Clamp(i0 + 1, 0, mono.Length - 1);
                float t = srcPos - i0;
                mono16k[i] = Mathf.Lerp(mono[i0], mono[i1], t);
            }
        }

        var pcm = new byte[dstFrames * 2];
        int p = 0;
        for (int i = 0; i < dstFrames; i++)
        {
            var clamped = Mathf.Clamp(mono16k[i], -1f, 1f);
            short s = (short)Mathf.RoundToInt(clamped * short.MaxValue);
            pcm[p++] = (byte)(s & 0xFF);
            pcm[p++] = (byte)((s >> 8) & 0xFF);
        }

        return pcm;
    }
}
