using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Windows.Speech;
using System.Collections;
using UnityEngine.Events;

namespace VAI
{
    public class VadWakeWordDetect : MonoBehaviour
    {
        [Header("Wake Word Settings")]
        [Tooltip("Words to detect to start interaction.")]
        public string[] keywords = { "Hi there" };
        public ConfidenceLevel confidenceLevel = ConfidenceLevel.Medium;

        [Header("Silence Detection")]
        [Tooltip("Volume level below which is considered silence.")]
        public float silenceVolumeThreshold = 2f;
        [Tooltip("How long to wait in silence before triggering the end of speech (in seconds).")]
        public float silenceTimeThreshold = 2.0f;
        [Tooltip("A grace period after starting monitoring before silence can be detected (in seconds).")]
        public float wakeBufferTime = 0.5f;
        [Tooltip("Maximum duration to listen for speech before automatically stopping (in seconds).")]
        public float maxRecordingTime = 15.0f;

        [Header("Optional")]
        public UnityEvent<float> OnVolumeChanged = new UnityEvent<float>();


        // --- Events ---
        // Provides the full event args, including confidence.
        public event Action<PhraseRecognizedEventArgs> OnWakeWordRecognized;
        // Signals that silence has been detected. Parameter indicates if it was during wake buffer time.
        public event Action<bool> OnSilenceDetected;

        // --- Public Properties ---
        public AudioClip MicrophoneClip { get; private set; }
        public string MicrophoneDevice { get; private set; }

        // --- Private State ---
        private KeywordRecognizer keywordRecognizer;
        private float[] _clipSampleData = new float[1024];
        private Coroutine _silenceDetectionCoroutine;
        private float _currentVolume;


        #region Public Methods

        public void Initialize()
        {
            if (Microphone.devices.Length == 0)
            {
                Debug.LogError("VAD Error: No microphone devices found.");
                return;
            }

            MicrophoneDevice = Microphone.devices[0];
            // Start the microphone in a loop for continuous audio data.
            MicrophoneClip = Microphone.Start(MicrophoneDevice, true, 10, 16000);
            Debug.Log($"VAD: Microphone started on {MicrophoneDevice}.");

            keywordRecognizer = new KeywordRecognizer(keywords, confidenceLevel);
            keywordRecognizer.OnPhraseRecognized += HandlePhraseRecognized;
            keywordRecognizer.Start();
            Debug.Log("VAD: Wake word recognizer started.");
        }

        public void Shutdown()
        {
            Debug.Log("VAD: Shutting down...");
            if (keywordRecognizer != null)
            {
                if (keywordRecognizer.IsRunning)
                {
                    keywordRecognizer.Stop();
                }
                keywordRecognizer.OnPhraseRecognized -= HandlePhraseRecognized;
                keywordRecognizer.Dispose();
                keywordRecognizer = null;
                Debug.Log("VAD: Wake word recognizer stopped and disposed.");
            }

            if (Microphone.IsRecording(MicrophoneDevice))
            {
                Microphone.End(MicrophoneDevice);
                Debug.Log("VAD: Microphone stopped.");
            }
            
            StopSilenceMonitoring();
        }

        public void StartSilenceMonitoring()
        {
            // Stop any existing coroutine to prevent duplicates.
            if (_silenceDetectionCoroutine != null)
            {
                StopCoroutine(_silenceDetectionCoroutine);
            }
            _silenceDetectionCoroutine = StartCoroutine(SilenceDetectionCoroutine());
        }

        public void StopSilenceMonitoring()
        {
            if (_silenceDetectionCoroutine != null)
            {
                StopCoroutine(_silenceDetectionCoroutine);
                _silenceDetectionCoroutine = null;
                Debug.Log("VAD: Stopped monitoring for silence.");
            }
        }

        public float GetCurrentVolume()
        {
            if (MicrophoneClip == null || !Microphone.IsRecording(MicrophoneDevice)) return 0;

            int micPosition = Microphone.GetPosition(MicrophoneDevice);
            int sampleCount = _clipSampleData.Length;

            int readPosition = micPosition - sampleCount;

            // To prevent native crashes from invalid read positions in the audio buffer,
            // especially during wrap-around scenarios, we clamp the read position.
            // If the calculated position is negative, we safely default to the
            // beginning of the buffer. This provides stability at the cost of
            // perfect accuracy for the first fraction of a second.
            if (readPosition < 0)
            {
                readPosition = 0;
            }

            MicrophoneClip.GetData(_clipSampleData, readPosition);
            
            float sum = _clipSampleData.Sum(sample => sample * sample);
            return Mathf.Sqrt(sum / _clipSampleData.Length) * 100;
        }

        #endregion

        private void Update()
        {
            _currentVolume = GetCurrentVolume();
            OnVolumeChanged.Invoke(_currentVolume);
        }
        

        private IEnumerator SilenceDetectionCoroutine()
        {
            Debug.Log("VAD: Started Silence Detection Coroutine.");
            
            float timeSinceLastSound = 0f;
            float recordingStartTime = Time.time;
            bool hasDetectedVolumeChange = false;
            bool isInWakeBufferTime = true;

            // Phase 1: Wake buffer time - check for volume changes
            while (isInWakeBufferTime && Time.time - recordingStartTime < wakeBufferTime)
            {
                // Check for max recording time
                if (Time.time - recordingStartTime > maxRecordingTime)
                {
                    Debug.Log($"VAD: Max recording time of {maxRecordingTime}s reached during wake buffer. Forcing stop.");
                    OnSilenceDetected?.Invoke(true);
                    _silenceDetectionCoroutine = null;
                    yield break;
                }

                if (_currentVolume > silenceVolumeThreshold)
                {
                    hasDetectedVolumeChange = true;
                    //Debug.Log("VAD: Volume change detected during wake buffer time.");
                }

                yield return null;
            }

            // If no volume change detected during wake buffer time, trigger silence detection
            if (!hasDetectedVolumeChange)
            {
                Debug.Log($"VAD: No volume change detected during wake buffer time of {wakeBufferTime}s. Triggering silence detection.");
                OnSilenceDetected?.Invoke(true);
                _silenceDetectionCoroutine = null;
                yield break;
            }

            // Phase 2: Normal silence detection after wake buffer time
            isInWakeBufferTime = false;
            timeSinceLastSound = 0f;

            while (true)
            {
                // Check for max recording time
                if (Time.time - recordingStartTime > maxRecordingTime)
                {
                    Debug.Log($"VAD: Max recording time of {maxRecordingTime}s reached. Forcing stop.");
                    OnSilenceDetected?.Invoke(false);
                    _silenceDetectionCoroutine = null;
                    yield break;
                }
                
                if (_currentVolume > silenceVolumeThreshold)
                {
                    // Sound was detected, so reset the silence timer.
                    timeSinceLastSound = 0f;
                }
                else
                {
                    // No sound, so increment the silence timer.
                    timeSinceLastSound += Time.deltaTime;
                }

                // Check if the silence threshold has been met.
                if (timeSinceLastSound > silenceTimeThreshold)
                {
                    Debug.Log($"VAD: Silence detected for over {silenceTimeThreshold}s.");
                    OnSilenceDetected?.Invoke(false);
                    _silenceDetectionCoroutine = null;
                    yield break; 
                }
                yield return null;
            }
        }

        private void HandlePhraseRecognized(PhraseRecognizedEventArgs args)
        {
            Debug.Log($"VAD: Detected wake word '{args.text}' with confidence {args.confidence}.");
            // Fire the event to notify the manager.
            OnWakeWordRecognized?.Invoke(args);
        }

        private void OnDestroy()
        {
            // Ensure cleanup even if the object is destroyed unexpectedly.
            Shutdown();
        }
    }
}