using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
// using Whisper.Utils;          // no longer needed
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;

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

        // --- Azure: add these ---
        [Header("Azure Speech")]
        [Tooltip("Your Azure Speech resource key")]
        public string azureKey;
        public string azureEndpoint;
        public string azureLanguage = "en-US";
        private WhisperSpeechManager whisperManager;
        public string openAiApiKey;

        // ------------------------

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

            // --- Azure init ---
            /*if (!string.IsNullOrWhiteSpace(azureKey) && !string.IsNullOrWhiteSpace(azureEndpoint))
            {
                azureManager = new AzureSpeechManager(azureKey, azureEndpoint, azureLanguage);
            }
            else
            {
                Debug.LogWarning("AzureSpeechManager missing key/endpoint. Set them in the inspector.");
            }*/


            if (!string.IsNullOrWhiteSpace(openAiApiKey))
                whisperManager = new WhisperSpeechManager(openAiApiKey);


        }

        private void Start()
        {
            StartCoroutine(VoiceLoop());
        }

        private void Update()
        {
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
                    AudioClip tempClip = Microphone.Start(selectedMic, true, 1, sampleRate);
                    yield return null;

                    float waitTime = 0f;
                    bool voiceDetected = false;
                    volumeSamples.Clear();

                    while (waitTime < 10f && !voiceDetected)
                    {
                        if (IsVoiceDetected(tempClip, silenceThreshold))
                        {
                            voiceDetected = true;
                            Microphone.End(selectedMic);
                            StartRecording();
                            break;
                        }

                        waitTime += volumeCheckInterval;
                        yield return new WaitForSeconds(volumeCheckInterval);
                    }

                    if (!voiceDetected)
                    {
                        Microphone.End(selectedMic);
                        Debug.Log("No voice detected after waiting.");
                        yield return new WaitForSeconds(1f);
                        continue;
                    }
                }

                if (isRecording)
                {
                    if (IsVoiceDetected(recordedClip, silenceThreshold))
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

        private float GetSmoothedVolume(AudioClip clip)
        {
            float vol = GetMaxVolume(clip);
            if (volumeSamples.Count >= volumeSampleCount) volumeSamples.Dequeue();
            volumeSamples.Enqueue(vol);
            return volumeSamples.Average();
        }

        private void StartRecording()
        {
            if (isRecording || isProcessing) return;

            recordedClip = Microphone.Start(selectedMic, true, recordDuration, sampleRate);
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

            int position = Microphone.GetPosition(selectedMic);
            Microphone.End(selectedMic);
            isRecording = false;

            var hoveredObjects = pointingLogger.ObjectLogs;
            var hoveredSurfaces = pointingLogger.SurfaceLogs;

            Debug.Log("Stopped recording");

            if (position <= 0)
            {
                Debug.LogWarning("Recording too short — skipping processing.");
                return;
            }

            float[] data = new float[position];
            recordedClip.GetData(data, 0);

            float maxVol = 0f;
            foreach (var sample in data) maxVol = Mathf.Max(maxVol, Mathf.Abs(sample));
            if (maxVol < silenceThreshold)
            {
                Debug.LogWarning($"Audio too quiet (max volume = {maxVol}) — skipping transcription.");
                return;
            }

            AudioClip finalClip = AudioClip.Create("clip", position, recordedClip.channels, recordedClip.frequency, false);
            finalClip.SetData(data, 0);

            isProcessing = true;
            StartCoroutine(ProcessClip(finalClip));
        }


        private IEnumerator ProcessClip(AudioClip clip)
        {
            pointingLogger.StopRecordingTracking();

            if (whisperManager == null)
            {
                Debug.LogError("WhisperSpeechManager is not initialized. Check key.");
                isProcessing = false;
                yield break;
            }

            //var task = azureManager.GetTextAsync(clip);
            var task = whisperManager.GetTextAsync(clip);
            yield return new WaitUntil(() => task.IsCompleted);

            var res = task.Result; 

            if (res != null)
            {
                string userQuery = res.Text?.Trim();
                string loweredQuery = userQuery?.ToLowerInvariant();
                Debug.Log(loweredQuery);
                if (!string.IsNullOrWhiteSpace(userQuery) && !IsInvalidTranscription(userQuery))
                {
                    // 1) Collect pointing metadata
                    onNewBufferCommand(res, userQuery);

                    if (pendingCommand != null)
                    {
                        string cleaned = userQuery.Trim();

                        if (string.IsNullOrWhiteSpace(cleaned))
                        {
                            GiveFeedback("Please say your command clearly.", readyColor, startBeep);
                            pendingCommand = null;
                            isProcessing = false;
                            yield break;
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
                    Debug.Log("Ignored transcription (invalid/empty).");
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


        // --- UPDATED: accepts WhisperSpeechManager.WhisperResult instead of WhisperLocalResult ---
        private void onNewBufferCommand(WhisperSpeechManager.WhisperResult res, string userQuery)
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
            if (clip == null || clip.samples == 0) return 0f;

            int micPosition = Microphone.GetPosition(selectedMic);
            if (micPosition <= 0 || micPosition > clip.samples) return 0f;

            int sampleSize = 1024;
            float[] samples = new float[sampleSize];
            int startSample = Mathf.Max(0, micPosition - sampleSize);

            try { clip.GetData(samples, startSample); }
            catch { return 0f; }

            float max = 0f;
            foreach (var sample in samples) max = Mathf.Max(max, Mathf.Abs(sample));
            return max;
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
