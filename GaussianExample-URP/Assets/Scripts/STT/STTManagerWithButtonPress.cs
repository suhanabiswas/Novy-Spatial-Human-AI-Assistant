using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Whisper.Utils;

namespace Whisper.Samples
{
    public class STTManagerWithButtonPress : MonoBehaviour
    {
        public Button startRecording;
        public Button stopRecording;
        public Button submitCommandButton;
        public TextMeshProUGUI recordInstructionText;

        public WhisperManager manager;
        private AudioSource audioSource;
        private AudioClip recordedClip;
        private bool isRecording = false;
        private bool isProcessing = false;

        [Header("UI")]
        public TextMeshProUGUI outputText;

        public bool echoSound = true;
        public bool printLanguage = true;
        private string selectedMic;
        private string _buffer;

        private string userQuery;

        private int lastMicPosition = 0;

        private void Awake()
        {
            audioSource = gameObject.AddComponent<AudioSource>();

            // Attempt to auto-select the Oculus headset mic
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

            // Auto-select default mic
            if (Microphone.devices.Length > 0)
            {
                selectedMic = Microphone.devices[0];
                Debug.Log($"Using mic: {selectedMic}");
            }
            else
            {
                Debug.LogError("No microphone devices found.");
            }

            outputText.text = "Transcribed user command shown here..";
        }

        private void Update()
        {
            if (isRecording && !Microphone.IsRecording(selectedMic))
            {
                Debug.LogWarning("Microphone recording stopped unexpectedly. Auto-invoking StopRecording().");
                StopRecording();
            }
        }

        public void StartRecording()
        {
            if (isProcessing || isRecording) return;

            if (Microphone.IsRecording(selectedMic)) return;

            isRecording = true;
            outputText.text = "Recording...";

            recordedClip = Microphone.Start(selectedMic, false, 20, 16000); // Max 10 seconds
            lastMicPosition = 0;

            userQuery = null;
            recordInstructionText.text = "To stop recording, look at your palm and press mic button on the wrist.";

            Debug.Log($"Started recording from {selectedMic}");
        }

        public void StopRecording()
        {
            if (!isRecording) return;

            int position = Microphone.GetPosition(selectedMic);

            if (Microphone.IsRecording(selectedMic))
            {
                Microphone.End(selectedMic);
                Debug.Log("Microphone manually stopped.");
            }
            else
            {
                Debug.Log("Microphone was already stopped.");
            }

            isRecording = false;
            outputText.text = "Processing...";
            recordInstructionText.text = "To start recording, look at your palm and press mic button on the wrist.";
            isProcessing = true;

            if (echoSound && recordedClip != null)
            {
                audioSource.clip = recordedClip;
                audioSource.Play();
                outputText.text += "\nPlaying Back...";
            }

            Debug.Log($"Recorded length: {recordedClip.length}, samples: {recordedClip.samples}");
            StartCoroutine(ProcessAfterDelay(0.25f));
        }

        private IEnumerator ProcessAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);

            if (recordedClip == null || recordedClip.samples == 0)
            {
                Debug.LogWarning("Empty AudioClip!");
                outputText.text = "No audio captured. Try again.";
                isProcessing = false;
                yield break;
            }

            if (IsClipSilent(recordedClip))
            {
                Debug.LogWarning("Audio appears silent.");
                outputText.text = "Silence detected. Try again.";
                isProcessing = false;
                yield break;
            }

            yield return TranscribeAudio();
        }

        private IEnumerator TranscribeAudio()
        {
            _buffer = "";

            var task = manager.GetTextAsync(recordedClip);
            yield return new WaitUntil(() => task.IsCompleted);

            var res = task.Result;
            if (res == null || !outputText)
            {
                isProcessing = false;
                yield break;
            }

            var text = res.Result;
            outputText.text = text;
            userQuery = text;

            isProcessing = false;
        }

        private bool IsClipSilent(AudioClip clip)
        {
            float[] samples = new float[clip.samples];
            clip.GetData(samples, 0);

            float max = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float abs = Mathf.Abs(samples[i]);
                if (abs > max) max = abs;
            }

            Debug.Log($"Max amplitude in recorded clip: {max}");
            return max < 0.001f;
        }

        public string GetUserQuery()
        {
            return userQuery;
        }
    }
}
