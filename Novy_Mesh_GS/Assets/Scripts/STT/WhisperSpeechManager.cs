using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using UnityEngine;

public sealed class WhisperSpeechManager
{
    private readonly string _apiKey;
    private readonly HttpClient _http;

    public WhisperSpeechManager(string openAiKey)
    {
        _apiKey = openAiKey?.Trim();
        if (string.IsNullOrWhiteSpace(_apiKey))
            throw new ApplicationException("Missing OpenAI API key.");

        _http = new HttpClient();
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
    }

    public sealed class WhisperWord
    {
        [JsonPropertyName("word")]
        public string Text { get; set; }

        [JsonPropertyName("start")]
        public float StartSec { get; set; }

        [JsonPropertyName("end")]
        public float EndSec { get; set; }
    }

    public sealed class WhisperResult
    {
        [JsonPropertyName("text")]
        public string Text { get; set; }

        [JsonPropertyName("words")]
        public WhisperWord[] Words { get; set; }
    }

    /// <summary>
    /// Saves the AudioClip to a 16-bit mono PCM WAV and calls whisper-1 with verbose_json + word timestamps.
    /// </summary>
    public async Task<WhisperResult> GetTextAsync(AudioClip clip)
    {
        if (clip == null || clip.samples == 0)
            return new WhisperResult { Text = null, Words = Array.Empty<WhisperWord>() };

        string tempDir = string.IsNullOrEmpty(Application.temporaryCachePath)
            ? Application.persistentDataPath
            : Application.temporaryCachePath;

        Directory.CreateDirectory(tempDir);
        string wavPath = Path.Combine(tempDir, $"whisper_{Guid.NewGuid():N}.wav");
        WriteWav16kMono(clip, wavPath);

        try
        {
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent("whisper-1"), "model");
            form.Add(new StringContent("verbose_json"), "response_format");
            form.Add(new StringContent("word"), "timestamp_granularities[]");
            form.Add(new StreamContent(File.OpenRead(wavPath)), "file", Path.GetFileName(wavPath));

            using var resp = await _http.PostAsync("https://api.openai.com/v1/audio/transcriptions", form);
            string json = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                Debug.LogError($"Whisper API error: {resp.StatusCode} — {json}");
                return new WhisperResult { Text = null, Words = Array.Empty<WhisperWord>() };
            }

            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var result = JsonSerializer.Deserialize<WhisperResult>(json, opts);

            // Ensure non-null arrays for safety
            if (result == null) return new WhisperResult { Text = null, Words = Array.Empty<WhisperWord>() };
            if (result.Words == null) result.Words = Array.Empty<WhisperWord>();
            return result;
        }
        finally
        {
            try { File.Delete(wavPath); } catch { /* ignore */ }
        }
    }

    // --- WAV writer helper ---
    private static void WriteWav16kMono(AudioClip clip, string path)
    {
        float[] data = new float[clip.samples * clip.channels];
        clip.GetData(data, 0);

        // downmix to mono
        float[] mono = new float[clip.samples];
        if (clip.channels == 1)
            Array.Copy(data, mono, clip.samples);
        else
        {
            for (int i = 0; i < clip.samples; i++)
            {
                float sum = 0f;
                for (int c = 0; c < clip.channels; c++)
                    sum += data[i * clip.channels + c];
                mono[i] = sum / clip.channels;
            }
        }

        // resample to 16k
        int srcRate = clip.frequency;
        const int dstRate = 16000;
        float ratio = (float)dstRate / srcRate;
        int dstSamples = Mathf.Max(1, Mathf.RoundToInt(mono.Length * ratio));
        float[] resampled = new float[dstSamples];

        for (int i = 0; i < dstSamples; i++)
        {
            float srcPos = i / ratio;
            int i0 = Mathf.Clamp((int)Mathf.Floor(srcPos), 0, mono.Length - 1);
            int i1 = Mathf.Clamp(i0 + 1, 0, mono.Length - 1);
            float t = srcPos - i0;
            resampled[i] = Mathf.Lerp(mono[i0], mono[i1], t);
        }

        using var fs = new FileStream(path, FileMode.Create);
        using var bw = new BinaryWriter(fs);

        int sampleCount = resampled.Length;
        int byteRate = dstRate * 2; // mono * 16-bit
        int subchunk2Size = sampleCount * 2;
        int chunkSize = 36 + subchunk2Size;

        // RIFF header
        bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        bw.Write(chunkSize);
        bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));

        // fmt chunk
        bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        bw.Write(16);             // PCM
        bw.Write((short)1);       // format
        bw.Write((short)1);       // channels
        bw.Write(dstRate);        // sample rate
        bw.Write(byteRate);       // byte rate
        bw.Write((short)2);       // block align
        bw.Write((short)16);      // bits per sample

        // data chunk
        bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        bw.Write(subchunk2Size);

        for (int i = 0; i < sampleCount; i++)
        {
            short s = (short)Mathf.Clamp(resampled[i] * short.MaxValue, short.MinValue, short.MaxValue);
            bw.Write(s);
        }
    }
}
