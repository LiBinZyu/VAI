using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters; // Required for StringEnumConverter
using NativeWebSocket;

namespace VAI
{
    // ASR API数据结构
    [System.Serializable]
    public class TaskRequest
    {
        public Header header;
        public Payload payload;
    }

    [System.Serializable]
    public class Header
    {
        public string action;
        public string task_id;
        public string streaming = "duplex";
    }

    [System.Serializable]
    public class Payload
    {
        public string task_group = "audio";
        public string task = "asr";
        public string function = "recognition";
        public string model = "paraformer-realtime-v2";
        public Parameters parameters;
        public Input input = new Input();
    }

    [System.Serializable]
    public class Parameters
    {
        [Tooltip("Audio format.")]
        public string format = "wav";
        [Tooltip("Audio sample rate.")]
        public int sample_rate = 16000;

        [Tooltip("Optional vocabulary id for custom hot words.")]
        public string vocabulary_id;

        public enum SupportedLanguage
        {
            [InspectorName("zh (普通话、上海话、吴语、闽南语、东北话、甘肃话、贵州话、河南话、湖北话、湖南话、江西话、宁夏话、山西话、陕西话、山东话、四川话、天津话、云南话、粤语)")]
            zh,
            [InspectorName("en (English)")]
            en,
            [InspectorName("ja (Japanese)")]
            ja,
            [InspectorName("yue (廣東話)")]
            yue,
            [InspectorName("ko (Korean)")]
            ko,
            [InspectorName("de (German)")]
            de,
            [InspectorName("fr (French)")]
            fr,
            [InspectorName("ru (Russian)")]
            ru
        }

        [Tooltip("Specifies the recognition language to improve accuracy.")]
        public List<SupportedLanguage> language_hints = new List<SupportedLanguage>();

        [Tooltip("Enable semantic punctuation. If false, VAD-based punctuation is used.")]
        public bool semantic_punctuation_enabled = false;

        [Tooltip("Prevents VAD from cutting sentences too long. Only effective when semantic_punctuation_enabled is false.")]
        public bool multi_threshold_mode_enabled = false;

        [Tooltip("VAD silence duration threshold in milliseconds (200-6000). Only effective when semantic_punctuation_enabled is false.")]
        [Range(200, 6000)]
        public int max_sentence_silence = 800;

        [Tooltip("Filters out disfluent words like 'um', 'uh', etc.")]
        public bool disfluency_removal_enabled = true;

        [Tooltip("Automatically adds punctuation to the recognition results.")]
        public bool punctuation_prediction_enabled = true;

        [Tooltip("Keeps the connection alive during long periods of silence by sending silent audio.")]
        public bool heartbeat = false;

        [Tooltip("Enables Inverse Text Normalization, converting Chinese numbers to Arabic numerals.")]
        public bool inverse_text_normalization_enabled = true;
    }


    [System.Serializable]
    public class Input { }

    [System.Serializable]
    public class BaseResponse
    {
        public ResponseHeader header;
    }

    [System.Serializable]
    public class ResponseHeader
    {
        public string @event;
        public int status_code;
        public string task_id;
        public string error_message;
    }

    [System.Serializable]
    public class RecognitionResponse
    {
        public ResponseHeader header;
        public ResponsePayload payload;
    }


    [System.Serializable]
    public class ResponsePayload
    {
        public Output output;
    }

    [System.Serializable]
    public class Output
    {
        public Sentence sentence;
    }

    [System.Serializable]
    public class Word
    {
        public string text;
        public string punctuation;
    }

    [System.Serializable]
    public class Sentence
    {
        public string text;
        public long? begin_time;
        public long? end_time;//aliyun may returns end_time as null
        public Word[] words;
        public bool sentence_end;
    }

    public class AsrController : MonoBehaviour
    {
        [Header("API Configuration")]
        [Tooltip("Aliyun DashScope API Key. Uses environment variable DASHSCOPE_API_KEY if empty.")]
        public string apiKey = "DASHSCOPE_API_KEY";

        [Header("ASR Parameters")]
        [Tooltip("Fine-tune the speech recognition parameters.")]
        public Parameters asrParameters = new Parameters();

        public float maxRecordingTime = 20f;

        [Header("Silence Threshold (seconds)")]
        [Tooltip("静音超时时间（秒），到达后自动回到Idle")]
        public float silenceThreshold = 2.0f;

        [Header("VAD script")]
        [Tooltip("VAD script")]
        public SherpaOnnxKeywords vadModule;

        // Manager interface events
        public event Action<Sentence> OnRecognitionStreaming;
        public event Action<string> Error;

        // 新增：ASR流程事件
        // 事件声明移至下方“公共参数和事件”处，避免重复

        // Private state
        private WebSocket _websocket;
        private string _microphoneDevice;
        private AudioClip _recordedClip;

        // Task control
        private CancellationTokenSource _currentTaskCts;
        private bool _isTaskStarted;
        private bool _isTaskFinished;
        private bool _isProcessing = false;

        // ASR采集起点
        private int _asrStartSampleIndex = 0;
        private int _lastSentSampleIndex = 0;

        // 公共参数和事件
        public float SilenceTimeoutSeconds { get; set; } = 2.0f;
        public event Action OnSilenceTimeout;
        public event Action OnMaxRecordingTimeReached;
        public string LastAsrResult { get; private set; } = "";

        private void Awake()
        {
            // 保证silenceThreshold和SilenceTimeoutSeconds同步
            SilenceTimeoutSeconds = silenceThreshold;
        }

        // Recognition result
        private string _lastFinalTranscript = "";

        // Effective API Key
        private string _effectiveApiKey;


        #region Manager Interface Implementation

        public void StartRecognition()
        {
            if (_isProcessing)
            {
                Debug.LogWarning("ASR is already processing, please do not start again.");
                return;
            }

            // 记录唤醒点采集帧索引
            _asrStartSampleIndex = vadModule != null ? vadModule.CurrentSampleIndex : 0;
            _lastSentSampleIndex = _asrStartSampleIndex;

            Debug.Log($"ASR: Starting recognition from sample index {_asrStartSampleIndex}");
            _ = StartStreamingAsync();
        }

        public void StopRecognition()
        {
            Debug.Log("ASR: Stopping recognition");
            _currentTaskCts?.Cancel();
            // 清空麦克风缓存数据
            vadModule?.ClearAudioBuffer();
        }

        #endregion

        #region Core ASR Logic

        private async Task StartStreamingAsync()
        {
            try
            {
                _isProcessing = true;
                _currentTaskCts = new CancellationTokenSource();
                var token = _currentTaskCts.Token;

                await ExecuteRecognitionPipeline(token);
            }
            catch (OperationCanceledException)
            {
                Debug.Log("ASR task was canceled.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"ASR task failed: {ex.Message}");
                var errorMsg = FormatUserFriendlyError(ex);
                UnityMainThreadDispatcher.Instance().Enqueue(() => Error?.Invoke(errorMsg));
            }
            finally
            {
                _isProcessing = false;
                CleanupWebSocket();
            }
        }

        private async Task ExecuteRecognitionPipeline(CancellationToken cancellationToken)
        {
            // 1. Validate and Initialize
            await ValidateAndInitialize(cancellationToken);

            // 2. Establish WebSocket Connection
            await EstablishWebSocketConnection(cancellationToken);

            // 3. Start ASR Task
            string taskId = System.Guid.NewGuid().ToString("N");
            await StartAsrTask(taskId, cancellationToken);

            // 4. Stream Audio Data
            await StreamAudioData(cancellationToken);

            // 5. Finish Task and Wait for Final Result
            await FinishAsrTask(taskId, cancellationToken);
        }

        private async Task ValidateAndInitialize(CancellationToken cancellationToken)
        {
            // Reset state
            _lastFinalTranscript = "";
            _isTaskStarted = false;
            _isTaskFinished = false;

            if (string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable(apiKey)))
            {
                throw new InvalidOperationException("API Key not found, please add DASHSCOPE_API_KEY to environment variables");
            }
            else
            {
                _effectiveApiKey = System.Environment.GetEnvironmentVariable(apiKey);
            }

            // Validate microphone
            Debug.Log($"[ASR Validate] vadModule is null: {vadModule == null}, IsMicActive: {vadModule?.IsMicActive}");
            if (vadModule == null || !vadModule.IsMicActive)
            {
                throw new InvalidOperationException("Microphone device is not available.");
            }

            // 不再依赖 _recordedClip 和 _microphoneDevice
            Debug.Log("ASR initialization validation passed.");
        }

        private async Task EstablishWebSocketConnection(CancellationToken cancellationToken)
        {
            string url = "wss://dashscope.aliyuncs.com/api-ws/v1/inference/";
            var headers = new Dictionary<string, string>
            {
                { "Authorization", $"Bearer {_effectiveApiKey}" },
                { "X-DashScope-DataInspection", "enable" }
            };

            _websocket = new WebSocket(url, headers);
            ConfigureWebSocketEvents();

            _websocket.Connect();

            // Wait for connection (max 10 seconds)
            float timeout = 10f;
            while (_websocket.State != WebSocketState.Open && timeout > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(100, cancellationToken);
                timeout -= 0.1f;
            }

            if (_websocket.State != WebSocketState.Open)
            {
                throw new TimeoutException("WebSocket connection timed out.");
            }

            Debug.Log("WebSocket connection established successfully.");
        }



        private void ConfigureWebSocketEvents()
        {
            _websocket.OnOpen += () => Debug.Log("[ASR] WebSocket connection opened.");
            _websocket.OnClose += (e) => Debug.Log($"[ASR] WebSocket connection closed: {e}");
            _websocket.OnError += (e) =>
            {
                Debug.LogError($"[ASR] WebSocket error: {e}");
                UnityMainThreadDispatcher.Instance().Enqueue(() => Error?.Invoke("Network connection failed."));
                _isTaskFinished = true;
            };
            _websocket.OnMessage += (bytes) =>
            {
                Debug.Log($"[ASR] WebSocket received message, length: {bytes.Length}");
                HandleWebSocketMessage(bytes);
            };
        }

        private async Task StartAsrTask(string taskId, CancellationToken cancellationToken)
        {
            // We need to manually add the 'format' and 'sample_rate' to the parameters object
            // because they are not part of the 'asrParameters' from the inspector.
            var payloadParams = new
            {
                format = "pcm",
                sample_rate = 16000,
                vocabulary_id = asrParameters.vocabulary_id,
                language_hints = asrParameters.language_hints,
                semantic_punctuation_enabled = asrParameters.semantic_punctuation_enabled,
                multi_threshold_mode_enabled = asrParameters.multi_threshold_mode_enabled,
                max_sentence_silence = asrParameters.max_sentence_silence,
                disfluency_removal_enabled = asrParameters.disfluency_removal_enabled,
                punctuation_prediction_enabled = asrParameters.punctuation_prediction_enabled,
                heartbeat = asrParameters.heartbeat,
                inverse_text_normalization_enabled = asrParameters.inverse_text_normalization_enabled,
            };

            var request = new
            {
                header = new { action = "run-task", task_id = taskId, streaming = "duplex" },
                payload = new
                {
                    task_group = "audio",
                    task = "asr",
                    function = "recognition",
                    model = "paraformer-realtime-v2",
                    parameters = payloadParams,
                    input = new { }
                }
            };

            var serializerSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                Converters = new List<JsonConverter> { new StringEnumConverter() }
            };
            string json = JsonConvert.SerializeObject(request, Formatting.Indented, serializerSettings);

            // *** DEBUG LOG TO VERIFY PARAMETERS ***
            Debug.Log($"[ASR] Sending run-task command:\n{json}");

            await _websocket.SendText(json);

            await WaitForCondition(() => _isTaskStarted, 10f, "Waiting for task to start", cancellationToken);
            Debug.Log("ASR task started successfully.");
        }


        private async Task StreamAudioData(CancellationToken cancellationToken)
        {
            Debug.Log("Starting to stream audio data.");

            const int chunkSize = 1024;
            const float silenceThreshold = 0.003f; // 静音判定阈值
            float silenceTimeoutSeconds = SilenceTimeoutSeconds;
            int silenceSampleCount = 0;
            int silenceSampleLimit = (int)(silenceTimeoutSeconds * 16000);

            float elapsed = 0f;
            float sampleRate = 16000f;

            while (true)
            {
                // 录音时长超限
                if (elapsed >= maxRecordingTime)
                {
                    Debug.Log("[ASR] maxRecordingTime reached, finishing ASR task.");
                    OnMaxRecordingTimeReached?.Invoke();
                    StopRecognition();
                    break;
                }

                float[] newData = null;
                int newLastSampleIndex = 0;
                if (vadModule != null)
                {
                    newData = vadModule.GetPcmDataSince(_lastSentSampleIndex, out newLastSampleIndex);
                }

                if (newData != null && newData.Length > 0)
                {
                    int offset = 0;
                    while (offset < newData.Length)
                    {
                        int sendLen = Math.Min(chunkSize, newData.Length - offset);
                        float[] chunk = new float[sendLen];
                        Array.Copy(newData, offset, chunk, 0, sendLen);

                        byte[] bytes = ConvertSamplesToPcmBytes(chunk);

                        float max = float.MinValue, min = float.MaxValue;
                        foreach (var v in chunk)
                        {
                            if (v > max) max = v;
                            if (v < min) min = v;
                        }
                        Debug.Log($"[ASR] Sending audio chunk, samples: {sendLen}, max: {max}, min: {min}");

                        // 静音检测
                        bool isSilent = true;
                        for (int i = 0; i < chunk.Length; i++)
                        {
                            if (Mathf.Abs(chunk[i]) > silenceThreshold)
                            {
                                isSilent = false;
                                break;
                            }
                        }
                        if (isSilent)
                        {
                            silenceSampleCount += chunk.Length;
                        }
                        else
                        {
                            silenceSampleCount = 0;
                        }
                        if (silenceSampleCount >= silenceSampleLimit)
                        {
                            Debug.Log("[ASR] Silence timeout reached, finishing ASR task.");
                            OnSilenceTimeout?.Invoke();
                            StopRecognition();
                            break;
                        }

                        if (_websocket != null && _websocket.State == WebSocketState.Open)
                        {
                            await _websocket.Send(bytes);
                        }

                        offset += sendLen;
                        elapsed += sendLen / sampleRate;
                    }
                    _lastSentSampleIndex = newLastSampleIndex;
                }
                else
                {
                    await Task.Delay(50, cancellationToken);
                    elapsed += 0.05f;
                }
            }

            Debug.Log("Audio data streaming finished.");
        }



        // 已废弃的 AudioClip 相关方法，全部移除

        private async Task FinishAsrTask(string taskId, CancellationToken cancellationToken)
        {
            var request = new
            {
                header = new
                {
                    action = "finish-task",
                    task_id = taskId,
                    streaming = "duplex"
                },
                payload = new
                {
                    input = new { }
                }
            };


            string json = JsonConvert.SerializeObject(request);
            await _websocket.SendText(json);

            // Wait for the task to finish
            await WaitForCondition(() => _isTaskFinished, maxRecordingTime, "Waiting for task to finish", cancellationToken);
            Debug.Log("ASR task finished.");
        }

        #endregion

        #region WebSocket Message Handling

        private void HandleWebSocketMessage(byte[] bytes)
        {
            try
            {
                var message = System.Text.Encoding.UTF8.GetString(bytes);

                var baseResponse = JsonConvert.DeserializeObject<BaseResponse>(message);
                if (baseResponse?.header == null) return;

                switch (baseResponse.header.@event)
                {
                    case "task-started":
                        _isTaskStarted = true;
                        Debug.Log("ASR task has started.");
                        break;

                    case "result-generated":
                    case "task-finished":
                        if (baseResponse.header.@event == "task-finished")
                        {
                            _isTaskFinished = true;
                        }
                        HandleRecognitionResult(message);
                        break;

                    case "task-failed":
                        _isTaskFinished = true;
                        string errorMessage = baseResponse.header.error_message ?? "ASR task failed";
                        Debug.LogError($"ASR task failed: {errorMessage}");
                        UnityMainThreadDispatcher.Instance().Enqueue(() => Error?.Invoke(errorMessage));
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error handling WebSocket message: {ex.Message}");
            }
        }

        private void HandleRecognitionResult(string message)
        {
            try
            {
                Debug.Log($"[ASR] HandleRecognitionResult raw message: {message}");

                var response = JsonConvert.DeserializeObject<RecognitionResponse>(message);
                var sentence = response?.payload?.output?.sentence;

                if (sentence != null && !string.IsNullOrEmpty(sentence.text))
                {
                    Debug.Log($"[ASR] Recognition result: {sentence.text}");
                    UnityMainThreadDispatcher.Instance().Enqueue(() =>
                    {
                        OnRecognitionStreaming?.Invoke(sentence);
                    });

                    // 检查静音（end_time不为null）或句子结束
                    if (sentence.end_time != null || sentence.sentence_end)
                    {
                        Debug.Log("[ASR] Detected end_time or sentence_end, finishing ASR task.");
                        StopRecognition();
                    }
                }
                else
                {
                    Debug.Log("[ASR] Recognition result: <empty or null>");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Error handling recognition result: {ex.Message}");
            }
        }

        #endregion

        #region Utility Methods

        private async Task WaitForCondition(Func<bool> condition, float timeoutSeconds, string description, CancellationToken cancellationToken)
        {
            float elapsed = 0f;
            while (!condition() && elapsed < timeoutSeconds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(100, cancellationToken);
                elapsed += 0.1f;
            }

            if (!condition())
            {
                throw new TimeoutException($"{description} timed out.");
            }
        }

        private void CleanupWebSocket()
        {
            if (_websocket != null)
            {
                if (_websocket.State == WebSocketState.Open)
                {
                    _ = _websocket.Close();
                }
                _websocket = null;
            }
        }

        private string FormatUserFriendlyError(Exception ex)
        {
            return ex switch
            {
                TimeoutException => "Connection timed out, please check your network.",
                InvalidOperationException => "Device error, please try again.",
                _ => "Processing failed, please try again."
            };
        }

        public static byte[] ConvertSamplesToPcmBytes(float[] samples)
        {
            byte[] pcmData = new byte[samples.Length * 2];
            int byteIndex = 0;

            foreach (var sample in samples)
            {
                short intSample = (short)(sample * short.MaxValue);
                byte[] sampleBytes = System.BitConverter.GetBytes(intSample);
                pcmData[byteIndex++] = sampleBytes[0];
                pcmData[byteIndex++] = sampleBytes[1];
            }
            return pcmData;
        }

        #endregion

        #region Unity Lifecycle

        private void Update()
        {
            // Dispatch WebSocket message queue
            if (_websocket != null && _websocket.State == WebSocketState.Open)
            {
                _websocket.DispatchMessageQueue();
            }
        }

        private void OnDestroy()
        {
            Debug.Log("Cleaning up ASR controller...");
            CleanupWebSocket();
            _currentTaskCts?.Cancel();
            _currentTaskCts?.Dispose();
        }

        #endregion
    }

    #region Unity Main Thread Dispatcher
    public class UnityMainThreadDispatcher : MonoBehaviour
    {
        private static UnityMainThreadDispatcher _instance;
        private readonly System.Collections.Concurrent.ConcurrentQueue<System.Action> _executionQueue =
            new System.Collections.Concurrent.ConcurrentQueue<System.Action>();

        public static UnityMainThreadDispatcher Instance()
        {
            if (_instance == null)
            {
                var go = new GameObject("MainThreadDispatcher");
                _instance = go.AddComponent<UnityMainThreadDispatcher>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }

        void Update()
        {
            while (_executionQueue.TryDequeue(out System.Action action))
            {
                action?.Invoke();
            }
        }

        public void Enqueue(System.Action action)
        {
            _executionQueue.Enqueue(action);
        }

        public async Task EnqueueAsync(System.Action action)
        {
            var tcs = new TaskCompletionSource<bool>();

            _executionQueue.Enqueue(() =>
            {
                try
                {
                    action?.Invoke();
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });

            await tcs.Task;
        }
    }
    #endregion
}
