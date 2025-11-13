using System.Collections;
using System;

using UnityEngine;
using TMPro;
using UnityEngine.UI;
// using Whisper.Utils;          // no longer needed
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;

// NEW: NAudio usings
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Whisper.Samples
{
    public class BufferedCommand
    {
        public string command;
        public string pointedObject;
        public float[] pointedPosition;
        public string pointedSurfaceObject;
    }

    public class STTManager : MonoBehaviour
    {
        public BufferedCommand pendingCommand = null;
        bool isAwaitingConfirmation = false;
        public bool isAwaitingChangeConfirmation = false;

        public QueryInputHandler queryHandler;
        public LLMResponseHandler responseHandler;


        [Header("Azure Speech")]
        [Tooltip("Your Azure Speech resource key")]
        public string azureKey;
        public string azureEndpoint;
        public string azureLanguage = "en-US";
        private AzureSpeechManager azureManager;


        public QueryInputHandler queryInputHandler;

        public GameObject MainCamera;
        public GameObject voiceCommandFeedbackWindow;
        public TextMeshProUGUI recordInstructionText;
        public Image backgroundPanel;

        public AudioClip startBeep;
        public AudioClip stopBeep;
        public AudioClip successBeep;

        public float silenceThreshold = 0.01f;
        public float silenceDurationToStop = 5f;

        private AudioClip recordedClip;
        private AudioSource audioSource;
        private bool isRecording = false;
        private bool isProcessing = false;
        private string selectedMic;
        private string userQuery;
        private float silenceTimer = 0f;
        private int sampleRate = 16000;
        private int recordDuration = 60;

        private Queue<float> volumeSamples = new Queue<float>();
        private int volumeSampleCount = 5;
        private float volumeCheckInterval = 0.05f;

        private Color listeningColor;
        private Color processingColor;
        private Color readyColor;
        private Color errorColor;

        private Vector3 velocity;
        public float smoothTime = 0.3f;
        public float maxSpeed = Mathf.Infinity;

        [SerializeField] private PointingEventLogger pointingLogger;


        // ======== NAudio: rolling capture ========
        private NAudioMic naudioMic;

        #region NAudio Microphone (replacement for Unity Microphone, because it causes ALOT of lags and issues)
        private class NAudioMic
        {
            public readonly int SampleRate;
            public readonly int Channels;

            private readonly object _lock = new object();
            private WaveInEvent _waveIn;
            private readonly List<float> _ring = new List<float>(1024 * 64);
            private int _recordStartIndex = -1;

            public NAudioMic(int sampleRate, int channels)
            {
                SampleRate = sampleRate;
                Channels = channels;
            }

            private bool _stopping = false; // differentiate intentional stop

            public void StartCapture(string deviceNameHint = null)
            {
                if (_waveIn != null) return;

                int deviceIndex = 0;
                if (!string.IsNullOrEmpty(deviceNameHint))
                {
                    for (int i = 0; i < WaveInEvent.DeviceCount; i++)
                    {
                        var caps = WaveInEvent.GetCapabilities(i);
                        if (!string.IsNullOrEmpty(caps.ProductName) &&
                            caps.ProductName.IndexOf(deviceNameHint, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            deviceIndex = i;
                            break;
                        }
                    }
                }

                _stopping = false;

                _waveIn = new WaveInEvent
                {
                    DeviceNumber = deviceIndex,
                    WaveFormat = new WaveFormat(SampleRate, 16, Channels)
                };

                _waveIn.DataAvailable += (s, a) =>
                {
                    try
                    {
                        var wb = new WaveBuffer(a.Buffer);
                        int sampleCount = a.BytesRecorded / 2; // 16-bit PCM

                        lock (_lock)
                        {
                            // Reserve space to reduce re-allocs (optional micro-optim)
                            int targetCount = _ring.Count + sampleCount;
                            if (targetCount > _ring.Capacity) _ring.Capacity = targetCount;

                            for (int i = 0; i < sampleCount; i++)
                            {
                                float v = wb.ShortBuffer[i] / 32768f;
                                _ring.Add(v);
                            }

                            // keep ~30s
                            int maxSamples = SampleRate * Channels * 30;
                            int overflow = _ring.Count - maxSamples;
                            if (overflow > 0)
                            {
                                _ring.RemoveRange(0, overflow);
                                if (_recordStartIndex >= 0)
                                {
                                    _recordStartIndex -= overflow;
                                    if (_recordStartIndex < 0) _recordStartIndex = 0;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"NAudio DataAvailable exception: {ex}");
                    }
                };

                _waveIn.RecordingStopped += (s, e) =>
                {
                    if (!_stopping)
                    {
                        Debug.LogWarning($"NAudio recording stopped (Reason: {e.Exception?.Message ?? "no exception"}). Restarting...");
                        try { _waveIn?.Dispose(); } catch { }
                        _waveIn = null;
                        UnityMainThreadDispatch(0.25f, () => StartCapture(deviceNameHint));
                    }
                };

                _waveIn.StartRecording();
            }

            public void StopCapture()
            {
                _stopping = true;
                if (_waveIn != null)
                {
                    try { _waveIn.StopRecording(); } catch { }
                    try { _waveIn.Dispose(); } catch { }
                    _waveIn = null;
                }
                lock (_lock)
                {
                    _ring.Clear();
                    _recordStartIndex = -1;
                }
            }

            public void Watchdog(string deviceNameHint = null)
            {
                if (!_stopping && _waveIn == null)
                {
                    StartCapture(deviceNameHint);
                }
            }

            private static void UnityMainThreadDispatch(float delaySeconds, Action action)
            {
                // In your MonoBehaviour, you can implement a simple dispatcher/coroutine.
                // If inside NAudioMic, call a provided callback from STTManager instead.
            }



            public void BeginLogicalRecording()
            {
                lock (_lock)
                {
                    _recordStartIndex = _ring.Count;
                }
            }

            public float[] EndLogicalRecording()
            {
                lock (_lock)
                {
                    if (_recordStartIndex < 0) return Array.Empty<float>();
                    int len = _ring.Count - _recordStartIndex;
                    if (len <= 0) return Array.Empty<float>();
                    var outArr = new float[len];
                    _ring.CopyTo(_recordStartIndex, outArr, 0, len);
                    _recordStartIndex = -1;
                    return outArr;
                }
            }

            public float GetRecentMaxVolume(int sampleCount = 1024)
            {
                lock (_lock)
                {
                    if (_ring.Count == 0) return 0f;
                    int start = Math.Max(0, _ring.Count - sampleCount);
                    float max = 0f;
                    // use first channel for volume
                    for (int i = start; i < _ring.Count; i += Channels)
                    {
                        float v = Math.Abs(_ring[i]);
                        if (v > max) max = v;
                    }
                    return max;
                }
            }
        }
        #endregion



        private void Awake()
        {
            audioSource = gameObject.AddComponent<AudioSource>();

            listeningColor = HexToColor("#475E2F");
            processingColor = HexToColor("#7A803E");
            readyColor = HexToColor("#324863");
            errorColor = HexToColor("#633335");

            foreach (var device in Microphone.devices)
            {
                Debug.Log($"Available mic: {device}");
                if (device.ToLower().Contains("oculus") || device.ToLower().Contains("headset"))
                {
                    selectedMic = device;
                    Debug.Log($"Found VR Headset Mic: {selectedMic}");
                    break;
                }
            }
            if (string.IsNullOrEmpty(selectedMic) && Microphone.devices.Length > 0)
            {
                selectedMic = Microphone.devices[0];
                Debug.Log($"Using default mic: {selectedMic}");
            }

            if (!string.IsNullOrWhiteSpace(azureKey) && !string.IsNullOrWhiteSpace(azureEndpoint))
            {
                azureManager = new AzureSpeechManager(azureKey, azureEndpoint, azureLanguage);
            }
            else
            {
                Debug.LogWarning("AzureSpeechManager missing key/endpoint. Set them in the inspector.");
            }

            //Start continuous NAudio capture (mono @ 16k)
            naudioMic = new NAudioMic(sampleRate, 1);
            naudioMic.StartCapture(selectedMic);

        }

        private void OnDestroy()
        {
            naudioMic?.StopCapture();
        }

        private void Start()
        {
            StartCoroutine(VoiceLoop());
        }

        private void Update()
        {
            // Keep the input alive if the OS glitches the device
            naudioMic?.Watchdog(selectedMic);

            if (MainCamera != null && voiceCommandFeedbackWindow != null)
            {
                Vector3 cameraPosition = MainCamera.transform.position;
                Transform camTransform = MainCamera.transform;

                float distance = 1.5f;
                Vector3 basePosition = cameraPosition + camTransform.forward * distance;

                float verticalOffset = -0.6f;
                float horizontalOffset = 0.4f;
                Vector3 offset = (camTransform.up * verticalOffset) + (camTransform.right * horizontalOffset);

                Vector3 targetPosition = basePosition + offset;

                voiceCommandFeedbackWindow.transform.position = Vector3.SmoothDamp(
                    voiceCommandFeedbackWindow.transform.position,
                    targetPosition,
                    ref velocity,
                    smoothTime,
                    maxSpeed,
                    Time.deltaTime);

                voiceCommandFeedbackWindow.transform.LookAt(camTransform.position);
                voiceCommandFeedbackWindow.transform.Rotate(0, 180f, 0);
            }
        }

        private IEnumerator VoiceLoop()
        {
            while (true)
            {
                if (!isRecording && !isProcessing)
                {
                    Debug.Log("Waiting for voice...");

                    // NAudio-based pre-roll detection (no temp Microphone.Start)
                    yield return null;

                    float waitTime = 0f;
                    bool voiceDetected = false;
                    volumeSamples.Clear();

                    while (waitTime < 10f && !voiceDetected)
                    {
                        if (IsVoiceDetectedNAudio(silenceThreshold))
                        {
                            voiceDetected = true;
                            StartRecording();
                            break;
                        }

                        waitTime += volumeCheckInterval;
                        yield return new WaitForSeconds(volumeCheckInterval);
                    }

                    if (!voiceDetected)
                    {
                        Debug.Log("No voice detected after waiting.");
                        yield return new WaitForSeconds(1f);
                        continue;
                    }
                }

                if (isRecording)
                {
                    // ===== Use NAudio rolling buffer for silence detection =====
                    if (IsVoiceDetectedNAudio(silenceThreshold))
                    {
                        silenceTimer = 0f;
                    }
                    else
                    {
                        silenceTimer += volumeCheckInterval;
                        if (silenceTimer >= 1.5f)
                        {
                            StopRecording();
                        }
                    }
                }

                yield return new WaitForSeconds(volumeCheckInterval);
            }
        }

        private bool IsVoiceDetected(AudioClip clip, float threshold)
        {
            float vol = GetSmoothedVolume(clip);
            return vol > threshold;
        }

        private bool IsVoiceDetectedNAudio(float threshold)
        {
            float vol = GetSmoothedVolumeNAudio();
            return vol > threshold;
        }

        private float GetSmoothedVolume(AudioClip clip)
        {
            float vol = GetMaxVolume(clip);
            if (volumeSamples.Count >= volumeSampleCount) volumeSamples.Dequeue();
            volumeSamples.Enqueue(vol);
            return volumeSamples.Average();
        }

        private float GetSmoothedVolumeNAudio()
        {
            float vol = naudioMic != null ? naudioMic.GetRecentMaxVolume(1024) : 0f;
            if (volumeSamples.Count >= volumeSampleCount) volumeSamples.Dequeue();
            volumeSamples.Enqueue(vol);
            return volumeSamples.Average();
        }

        private void StartRecording()
        {
            if (isRecording || isProcessing) return;

            // ===== NAudio: mark start in rolling buffer =====
            naudioMic.BeginLogicalRecording();
            pointingLogger.StartRecordingTracking();

            silenceTimer = 0f;
            isRecording = true;

            if (!isAwaitingConfirmation)
            {
                GiveFeedback("Ready for your command! Please start your command with \"Hey Novy...\"", listeningColor, startBeep);
            }

            Debug.Log("Started recording");

            if (queryHandler != null) queryHandler.pointedTargetObject = null;
            if (responseHandler != null) responseHandler.pointedTargetObject = null;
            queryHandler.pointedTargetPos = null;
            responseHandler.pointedTargetPos = null;
        }

        private void StopRecording()
        {
            if (!isRecording) return;

            // ===== NAudio: grab recorded segment =====
            var samples = naudioMic.EndLogicalRecording();
            isRecording = false;

            var hoveredObjects = pointingLogger.ObjectLogs;
            var hoveredSurfaces = pointingLogger.SurfaceLogs;

            Debug.Log("Stopped recording");

            int position = samples.Length;
            if (position <= 0)
            {
                Debug.LogWarning("Recording too short — skipping processing.");
                return;
            }

            float maxVol = 0f;
            foreach (var sample in samples) maxVol = Mathf.Max(maxVol, Mathf.Abs(sample));
            if (maxVol < silenceThreshold)
            {
                Debug.LogWarning($"Audio too quiet (max volume = {maxVol}) — skipping transcription.");
                return;
            }

            // Build a Unity AudioClip so the rest of the flow remains unchanged
            int channels = 1; // NAudioMic configured as mono
            var finalClip = AudioClip.Create("clip", position / channels, channels, sampleRate, false);
            finalClip.SetData(samples, 0);

            isProcessing = true;
            StartCoroutine(ProcessClip(finalClip));
        }

        /*private IEnumerator ProcessClip(AudioClip clip)
        {
            pointingLogger.StopRecordingTracking();

            if (azureManager == null)
            {
                Debug.LogError("AzureSpeechManager is not initialized. Check key/endpoint.");
                isProcessing = false;
                yield break;
            }

            var task = azureManager.GetTextAsync(clip);
            yield return new WaitUntil(() => task.IsCompleted);

            var res = task.Result; // AzureSpeechManager.AzureResult

            if (res != null)
            {
                isAwaitingConfirmation = true;

                string userQuery = res.Text?.Trim();
                string loweredQuery = userQuery?.ToLowerInvariant();

                // --- Wake word check ---
                bool hasWakeWord = !string.IsNullOrWhiteSpace(loweredQuery) &&
                                   (loweredQuery.Contains("hey") ||
                                    loweredQuery.Contains("hello") ||
                                    loweredQuery.Contains("hi") ||
                                    loweredQuery.Contains("novi") ||
                                    loweredQuery.Contains("novy") ||
                                    loweredQuery.Contains("nobi") ||
                                    loweredQuery.Contains("noby"));

                if (!string.IsNullOrWhiteSpace(userQuery) && !IsInvalidTranscription(userQuery) && hasWakeWord)
                {
                    // proceed as before (send or buffer command)
                    // If user says "send", submit the pending command
                    if (((loweredQuery.Contains("send")
                          || loweredQuery.Contains("sent")
                          || loweredQuery.Contains("yes")
                          || loweredQuery.Contains("yes send")
                          || loweredQuery.Contains("yes sent"))
                         || loweredQuery == "end"
                         || loweredQuery == "and"
                         || loweredQuery == "ant")
                        && pendingCommand != null)
                    {
                        if (queryHandler != null)
                        {
                            queryHandler.pointedTargetObject = pendingCommand.pointedObject;
                            queryHandler.pointedTargetPos = pendingCommand.pointedPosition;
                            queryHandler.pointedSurfaceObject = pendingCommand.pointedSurfaceObject;
                        }
                        if (responseHandler != null)
                        {
                            responseHandler.pointedTargetObject = pendingCommand.pointedObject;
                            responseHandler.pointedTargetPos = pendingCommand.pointedPosition;
                            responseHandler.pointedSurfaceObject = pendingCommand.pointedSurfaceObject;
                        }

                        queryInputHandler.OnSubmit(pendingCommand.command);
                        GiveFeedback($"Command sent and processing:\n\"{pendingCommand.command}\"", readyColor, successBeep);
                        isAwaitingConfirmation = false;
                        Debug.Log($"Sent with pointing data: {pendingCommand.command}");
                        pendingCommand = null;
                    }
                    else
                    {
                        onNewBufferCommand(res, userQuery);
                    }
                }
                else
                {
                    Debug.Log("Skipped invalid transcription.");
                }
            }
            else
            {
                Debug.LogWarning("Transcription failed.");
            }

            isProcessing = false;
            yield return new WaitForSeconds(3f);
        }*/

        private IEnumerator ProcessClip(AudioClip clip)
        {
            pointingLogger.StopRecordingTracking();

            if (azureManager == null)
            {
                Debug.LogError("AzureSpeechManager is not initialized. Check key/endpoint.");
                isProcessing = false;
                yield break;
            }

            var task = azureManager.GetTextAsync(clip);
            yield return new WaitUntil(() => task.IsCompleted);

            var res = task.Result; // AzureSpeechManager.AzureResult

            if (res != null)
            {
                string userQuery = res.Text?.Trim();
                string loweredQuery = userQuery?.ToLowerInvariant();
                Debug.Log(loweredQuery); 

                // Wake words
                bool hasWakeWord = !string.IsNullOrWhiteSpace(loweredQuery) &&
                   (loweredQuery.Contains("hey") ||
                    loweredQuery.Contains("hi") ||
                    loweredQuery.Contains("novi") ||
                    loweredQuery.Contains("novy") ||
                    loweredQuery.Contains("nobi") ||
                    loweredQuery.Contains("noby") ||
                    loweredQuery.Contains("movie") ||
                    loweredQuery.Contains("movy") ||
                    loweredQuery.Contains("navy") ||
                    loweredQuery.Contains("nevi") ||
                    loweredQuery.Contains("nevy") ||
                    loweredQuery.Contains("mobi") ||
                    loweredQuery.Contains("moby") ||
                    loweredQuery.Contains("finally"));

                // Only act if: non-empty, not invalid, and contains a wake word
                if (!string.IsNullOrWhiteSpace(userQuery) && !IsInvalidTranscription(userQuery) && hasWakeWord)
                {
                    // 1) Collect pointing metadata the same way as before
                    onNewBufferCommand(res, userQuery);    // this sets pendingCommand with pointed data

                    // 2) Immediately send (no "yes, send" step)
                    
                    if (pendingCommand != null)
                    {
                        string cleaned = CleanWakeWords(pendingCommand.command);

                        // If the user only said a wake word (no actual command)
                        if (string.IsNullOrWhiteSpace(cleaned))
                        {
                            // Just prompt the user to continue; do not submit
                            GiveFeedback("Please continue your command after saying \"Hey Novy...\" ", readyColor, startBeep);
                            Debug.Log("Wake word detected without a command. Prompting user to continue.");
                            pendingCommand = null;   // clear any buffered data
                            isAwaitingConfirmation = false; // keep flow simple (no confirm mode)
                            isProcessing = false;
                            yield return new WaitForSeconds(1f); // optional small pause
                            yield break; // end this processing cycle
                        }

                        if (queryHandler != null)
                        {
                            queryHandler.pointedTargetObject = pendingCommand.pointedObject;
                            queryHandler.pointedTargetPos = pendingCommand.pointedPosition;
                            queryHandler.pointedSurfaceObject = pendingCommand.pointedSurfaceObject;
                        }
                        if (responseHandler != null)
                        {
                            responseHandler.pointedTargetObject = pendingCommand.pointedObject;
                            responseHandler.pointedTargetPos = pendingCommand.pointedPosition;
                            responseHandler.pointedSurfaceObject = pendingCommand.pointedSurfaceObject;
                        }

                        queryInputHandler.OnSubmit(cleaned);
                        GiveFeedback($"Command sent and processing:\n\"{cleaned}\"", readyColor, successBeep);
                        Debug.Log($"Sent with pointing data: {cleaned}");
                        pendingCommand = null;
                    }
                }
                else
                {
                    Debug.Log("Ignored transcription (no wake word or invalid/empty).");
                }
            }
            else
            {
                Debug.LogWarning("Transcription failed.");
            }

            isProcessing = false;
            yield return new WaitForSeconds(3f);
        }


        private static string CleanWakeWords(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;

            // Accepted wake-word variants
            string[] wakes =
            {
        "hey", "hi",
        "novi", "novy",
        "nobi", "noby",
        "movie", "movy",
        "navy", "nevi", "nevy",
        "mobi", "moby", "finally"
    };

            string cleaned = text;

            // Remove each wake word and any punctuation/spaces immediately after
            foreach (var w in wakes)
            {
                cleaned = System.Text.RegularExpressions.Regex.Replace(
                    cleaned,
                    $@"\b{System.Text.RegularExpressions.Regex.Escape(w)}\b[:,!\.\s]*",
                    "",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            // Collapse extra spaces and trim
            cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s{2,}", " ").Trim();
            return cleaned;
        }


        // --- UPDATED: accepts AzureSpeechManager.AzureResult instead of LocalWhisperResult ---
        private void onNewBufferCommand(AzureSpeechManager.AzureResult res, string userQuery)
        {
            string pointedObject = null;
            float[] pointedPosition = null;
            string pointedSurfaceObject = null;

            if (res?.Words != null)
            {
                foreach (var w in res.Words)
                {
                    if (string.IsNullOrWhiteSpace(w.Text)) continue;

                    string word = w.Text.Trim().ToLowerInvariant();
                    double tokenStart = w.StartSec;
                    double tokenEnd = w.EndSec;

                    if (word == "here" || word == "there" || word == "this" || word == "it" || word == "that")
                    {
                        if (word == "this" || word == "it" || word == "that")
                        {
                            var candidateObjects = pointingLogger.ObjectLogs
                                .Where(log => log.timestamp >= tokenStart && log.timestamp <= tokenEnd)
                                .OrderByDescending(log => log.timestamp)
                                .ToList();

                            var closestObject = candidateObjects.FirstOrDefault();
                            if (closestObject != null)
                            {
                                Debug.Log($"'{word}' at {tokenStart:F2}-{tokenEnd:F2}s likely refers to object: '{closestObject.objectName}'");
                                pointingLogger.ShowOutline(closestObject.selectedObject, Color.green);
                                pointedObject = closestObject.objectName;
                            }
                            else
                            {
                                Debug.LogWarning($"'{word}' at {tokenStart:F2}-{tokenEnd:F2}s: No object log match.");
                            }
                        }

                        if (word == "here" || word == "there" || word == "this" || word == "that")
                        {
                            var candidateSurfaces = pointingLogger.SurfaceLogs
                                .Where(log => log.timestamp >= tokenStart && log.timestamp <= tokenEnd)
                                .OrderByDescending(log => log.timestamp)
                                .ToList();

                            var closestSurface = candidateSurfaces.FirstOrDefault();
                            if (closestSurface != null)
                            {
                                Debug.Log($"'{word}' at {tokenStart:F2}-{tokenEnd:F2}s likely refers to surface position: {closestSurface.position}");
                                pointingLogger.ShowMarker(closestSurface.position, closestSurface.normal, Color.green);
                                pointedPosition = new float[]
                                {
                                    closestSurface.position.x,
                                    closestSurface.position.y,
                                    closestSurface.position.z
                                };
                                pointedSurfaceObject = closestSurface.surfaceObject != null
                                    ? closestSurface.surfaceObject.name
                                    : "Unnamed Surface";
                                Debug.Log($"Surface object name: {pointedSurfaceObject}");
                            }
                            else
                            {
                                Debug.LogWarning($"'{word}' at {tokenStart:F2}-{tokenEnd:F2}s: No surface log match.");
                            }
                        }
                    }
                }
            }

            // Buffer the command and metadata
            pendingCommand = new BufferedCommand
            {
                command = userQuery,
                pointedObject = pointedObject,
                pointedPosition = pointedPosition,
                pointedSurfaceObject = pointedSurfaceObject
            };

           /* GiveFeedback(
                $"Your command is:\n\"{userQuery}\"\n\n" +
                "To send this command, please say 'Yes, send'.\n" +
                "Or simply repeat your full command again to change it.",
                readyColor,
                stopBeep);

            Debug.Log($"Buffered command: {userQuery}");*/
        }

        private bool IsInvalidTranscription(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return true;
            string lower = text.ToLowerInvariant();
            return lower.Contains("[blank_audio]") || lower.Contains("[silence]") || lower.Contains("[no speech]") ||
                   (lower.StartsWith("[") && lower.EndsWith("]"));
        }

        private float GetMaxVolume(AudioClip clip)
        {
            // NAudio path: ignore clip, get recent volume
            if (naudioMic == null) return 0f;
            return naudioMic.GetRecentMaxVolume(1024);
        }

        public void GiveFeedback(string message, Color color, AudioClip clip = null)
        {
            if (recordInstructionText != null) recordInstructionText.text = message;
            if (backgroundPanel != null) backgroundPanel.color = color;
            if (clip != null && audioSource != null) audioSource.PlayOneShot(clip);
        }

        private IEnumerator FadeBackground(Color fromColor, float duration = 1.5f)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                backgroundPanel.color = Color.Lerp(fromColor, Color.clear, elapsed / duration);
                elapsed += Time.deltaTime;
                yield return null;
            }
            backgroundPanel.color = Color.clear;
        }

        public void ClearOutput()
        {
            userQuery = null;
        }

        public Color HexToColor(string hex)
        {
            if (ColorUtility.TryParseHtmlString(hex, out Color color))
                return color;
            else
                return Color.magenta;
        }
    }
}
