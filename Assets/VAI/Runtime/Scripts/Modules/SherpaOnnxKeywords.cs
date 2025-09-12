using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;
using Eitan.SherpaOnnxUnity.Runtime;
using Eitan.SherpaOnnxUnity.Samples;

namespace VAI
{
    public class SherpaOnnxKeywords : MonoBehaviour
    {
        [Header("Keyword Spotting Settings")]
        [Tooltip("Use .txt file under StreamingAssets/ , e.g. n ǐ h ǎo k āng k āng @你好康康")]
        public String customKeywordsPath;

        [Tooltip("The ID of the model folder located in StreamingAssets/sherpa-onnx/models/")]
        [SerializeField]
        private string modelID = "sherpa-onnx-kws-zipformer-wenetspeech-3.3M-2024-01-01";

        private const int SampleRate = 16000;
        private const float KeywordsScore = 2.0f;
        private const float KeywordsThreshold = 0.25f;

        public static event Action<string> OnKeywordSpotted;

        public AudioClip MicrophoneClip { get; private set; }
        public string MicrophoneDevice { get; private set; }

        public bool IsMicActive => _micDevice != null && _micDevice.IsRecording;

        private Mic.Device _micDevice;
        private int _micBufferPosition = 0;
        private const int MIC_BUFFER_SECONDS = 10;

        private KeywordSpotting _keywordSpotting;
        private string _runtimeKeywordsPath;

        // 新增：缓存最近的音频帧
        private Queue<float> _pcmQueue = new Queue<float>(MIC_BUFFER_SECONDS * SampleRate);
        private int _totalSampleCount = 0; // 全局采集帧计数
        public int CurrentSampleIndex => _totalSampleCount;

        /// <summary>
        /// 获取最近 N 秒的音频数据（float[]），用于 ASR
        /// </summary>
        /// <param name="seconds">需要的秒数</param>
        /// <returns>float[] 音频数据</returns>
        public float[] GetLatestPcmData(float seconds)
        {
            int sampleCount = Mathf.Min((int)(seconds * SampleRate), _pcmQueue.Count);
            float[] data = new float[sampleCount];
            int startIdx = _pcmQueue.Count - sampleCount;
            int idx = 0;
            foreach (var sample in _pcmQueue)
            {
                if (idx >= startIdx)
                {
                    data[idx - startIdx] = sample;
                }
                idx++;
            }
            return data;
        }

        /// <summary>
        /// 获取自 lastSampleIndex 以来的新音频帧（全局采集帧计数），返回新数据和最新采集帧索引
        /// </summary>
        /// <param name="lastSampleIndex">上次发送的全局帧索引</param>
        /// <param name="newLastSampleIndex">返回最新帧索引</param>
        /// <returns>float[] 新音频数据</returns>
        public float[] GetPcmDataSince(int lastSampleIndex, out int newLastSampleIndex)
        {
            int available = _totalSampleCount;
            int newSamples = available - lastSampleIndex;
            if (newSamples <= 0)
            {
                newLastSampleIndex = available;
                return new float[0];
            }
            // 只保留队列长度范围内的数据
            int queueCount = _pcmQueue.Count;
            int startIdx = Math.Max(0, queueCount - newSamples);
            float[] data = new float[queueCount - startIdx];
            int idx = 0, i = 0;
            foreach (var sample in _pcmQueue)
            {
                if (idx >= startIdx)
                {
                    data[i++] = sample;
                }
                idx++;
            }
            newLastSampleIndex = available;
            return data;
        }

        #region Public Methods

        /// <summary>
        /// 清空音频缓存（ASR取消时调用）
        /// </summary>
        public void ClearAudioBuffer()
        {
            _pcmQueue.Clear();
            _totalSampleCount = 0;
            _micBufferPosition = 0;
        }

        public void Initialize()
        {
            var devices = Mic.AvailableDevices;
            Debug.Log($"[SherpaOnnxKeywords] Mic.AvailableDevices count: {devices.Count}");
            if (devices.Count == 0)
            {
                Debug.LogError("SherpaOnnxKeywords: No microphone devices found.");
                return;
            }

            _micDevice = devices[0];
            MicrophoneDevice = _micDevice.Name;
            Debug.Log($"[SherpaOnnxKeywords] Selected device: {_micDevice.Name}");

            if (!string.IsNullOrEmpty(customKeywordsPath))
            {
                _runtimeKeywordsPath = Path.Combine(Application.streamingAssetsPath, customKeywordsPath);
            }

            _keywordSpotting = new KeywordSpotting(modelID, SampleRate, KeywordsScore, KeywordsThreshold, _runtimeKeywordsPath);
            _keywordSpotting.OnKeywordDetected += HandleKeywordDetected;

            MicrophoneClip = AudioClip.Create("SherpaOnnx-ASR-Buffer", MIC_BUFFER_SECONDS * SampleRate, 1, SampleRate, false);

            _micDevice.OnFrameCollected += OnFrameCollected;
            _micDevice.StartRecording(SampleRate, 10);

            Debug.Log($"SherpaOnnxKeywords: Microphone '{MicrophoneDevice}' started with Mic.cs. IsRecording: {_micDevice.IsRecording}");
        }

        public void Shutdown()
        {
            Debug.Log("SherpaOnnxKeywords: Shutting down...");
            if (_micDevice != null)
            {
                _micDevice.StopRecording();
                _micDevice.OnFrameCollected -= OnFrameCollected;
                _micDevice = null;
            }

            if (MicrophoneClip != null)
            {
                Destroy(MicrophoneClip);
                MicrophoneClip = null;
            }

            if (_keywordSpotting != null)
            {
                _keywordSpotting.OnKeywordDetected -= HandleKeywordDetected;
                _keywordSpotting.Dispose();
                _keywordSpotting = null;
            }
        }

        #endregion

        private void OnFrameCollected(int sampleRate, int channelCount, float[] pcm)
        {
            if (MicrophoneClip == null) return;

            //Debug.Log($"[OnFrameCollected] sampleRate: {sampleRate}, channelCount: {channelCount}, pcm.Length: {pcm.Length}, IsRecording: {_micDevice?.IsRecording}");

            _keywordSpotting?.StreamDetect(pcm);

            MicrophoneClip.SetData(pcm, _micBufferPosition);
            _micBufferPosition = (_micBufferPosition + pcm.Length) % MicrophoneClip.samples;

            // 新增：将音频帧写入队列，保证队列长度不超过 MIC_BUFFER_SECONDS * SampleRate
            foreach (var sample in pcm)
            {
                if (_pcmQueue.Count >= MIC_BUFFER_SECONDS * SampleRate)
                {
                    _pcmQueue.Dequeue();
                }
                _pcmQueue.Enqueue(sample);
                _totalSampleCount++;
            }
        }

        private void HandleKeywordDetected(string keyword)
        {
            OnKeywordSpotted?.Invoke(keyword);
        }

        private void Update()
        {
            _keywordSpotting?.ProcessPendingEvents();
        }

        private void OnDestroy()
        {
            Shutdown();
        }
    }
}
