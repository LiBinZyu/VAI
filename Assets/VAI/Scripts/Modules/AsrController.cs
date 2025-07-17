using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json;
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
        public string format = "wav";
        public int sample_rate = 16000;
        public bool disfluency_removal_enabled = false;
        public string vocabulary_id;
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
    }

    public class AsrController : MonoBehaviour
    {
        [Header("Api")]
        [Tooltip("Aliyun DashScope API Key")]
        public string ApiKey;
        // [Tooltip("Optional vocabulary id for custom hot words")]
        // public string VocabularyId;
        
        [Header("VAD script")]
        [Tooltip("VAD script")]
        public VadWakeWordDetect vadModule;

        // Manager接口事件
        public event Action<string> OnRecognitionStreaming;
        public event Action<string> Error;

        // 私有状态
        private WebSocket _websocket;
        private string _microphoneDevice;
        private AudioClip _recordedClip;
        
        // 任务控制
        private CancellationTokenSource _currentTaskCts;
        private bool _isTaskStarted;
        private bool _isTaskFinished;
        private bool _isProcessing = false;
        
        // 识别结果
        private string _lastFinalTranscript = "";

        #region Manager接口实现

        public void StartRecognition()
        {
            if (_isProcessing)
            {
                Debug.LogWarning("ASR正在处理中，请勿重复启动");
                return;
            }

            Debug.Log("ASR: 开始识别");
            _ = StartStreamingAsync();
        }

        public void StopRecognition()
        {
            Debug.Log("ASR: 停止识别");
            _currentTaskCts?.Cancel();
        }

        #endregion

        #region 核心ASR逻辑

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
                Debug.Log("ASR任务被取消");
            }
            catch (Exception ex)
            {
                Debug.LogError($"ASR任务失败: {ex.Message}");
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
            // 1. 验证和初始化
            await ValidateAndInitialize(cancellationToken);

            // 2. 建立WebSocket连接
            await EstablishWebSocketConnection(cancellationToken);

            // 3. 启动ASR任务
            string taskId = System.Guid.NewGuid().ToString("N");
            await StartAsrTask(taskId, cancellationToken);

            // 4. 流式发送音频数据
            await StreamAudioData(cancellationToken);

            // 5. 完成任务并等待最终结果
            await FinishAsrTask(taskId, cancellationToken);
        }

        private async Task ValidateAndInitialize(CancellationToken cancellationToken)
        {
            // 重置状态
            _lastFinalTranscript = "";
            _isTaskStarted = false;
            _isTaskFinished = false;
            
            // 验证麦克风
            if (vadModule?.MicrophoneClip == null || !Microphone.IsRecording(vadModule.MicrophoneDevice))
            {
                throw new InvalidOperationException("麦克风设备不可用");
            }
            
            _recordedClip = vadModule.MicrophoneClip;
            _microphoneDevice = vadModule.MicrophoneDevice;
            
            Debug.Log("ASR初始化验证通过");
        }

        private async Task EstablishWebSocketConnection(CancellationToken cancellationToken)
        {
            string url = "wss://dashscope.aliyuncs.com/api-ws/v1/inference/";
            var headers = new Dictionary<string, string> 
            { 
                { "Authorization", $"Bearer {ApiKey}" }, 
                { "X-DashScope-DataInspection", "enable" } 
            };
            
            _websocket = new WebSocket(url, headers);
            ConfigureWebSocketEvents();
            
            _websocket.Connect();
            
            // 等待连接建立（最多10秒）
            float timeout = 10f;
            while (_websocket.State != WebSocketState.Open && timeout > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(100, cancellationToken);
                timeout -= 0.1f;
            }
            
            if (_websocket.State != WebSocketState.Open)
            {
                throw new TimeoutException("WebSocket连接超时");
            }
            
            Debug.Log("WebSocket连接建立成功");
        }

        private void ConfigureWebSocketEvents()
        {
            _websocket.OnOpen += () => Debug.Log("WebSocket连接打开");
            _websocket.OnClose += (e) => Debug.Log($"WebSocket连接关闭: {e}");
            _websocket.OnError += (e) => 
            { 
                Debug.LogError($"WebSocket错误: {e}"); 
                UnityMainThreadDispatcher.Instance().Enqueue(() => Error?.Invoke("网络连接失败"));
                _isTaskFinished = true; 
            };
            _websocket.OnMessage += HandleWebSocketMessage;
        }

        private async Task StartAsrTask(string taskId, CancellationToken cancellationToken)
        {
            var parameters = new Parameters();
            // if (!string.IsNullOrEmpty(VocabularyId))
            // {
            //     parameters.vocabulary_id = VocabularyId;
            // }
            
            var request = new TaskRequest
            {
                header = new Header { action = "run-task", task_id = taskId },
                payload = new Payload { parameters = parameters }
            };
            
            string json = JsonConvert.SerializeObject(request);
            await _websocket.SendText(json);
            
            // 等待任务启动确认
            await WaitForCondition(() => _isTaskStarted, 10f, "等待任务启动", cancellationToken);
            Debug.Log("ASR任务启动成功");
        }

        private async Task StreamAudioData(CancellationToken cancellationToken)
        {
            Debug.Log("开始流式发送音频数据");
            int lastPosition = Microphone.GetPosition(_microphoneDevice);
            const int minChunkSize = 1024;
            
            while (!cancellationToken.IsCancellationRequested)
            {
                int currentPosition = Microphone.GetPosition(_microphoneDevice);
                if (currentPosition != lastPosition)
                {
                    int length = CalculateAudioBufferLength(currentPosition, lastPosition);
                    
                    if (length >= minChunkSize)
                    {
                        await SendAudioChunk(lastPosition, length, cancellationToken);
                        lastPosition = currentPosition;
                    }
                }
                
                await Task.Delay(50, cancellationToken);
            }
            
            Debug.Log("音频数据流发送完成");
        }

        private int CalculateAudioBufferLength(int currentPosition, int lastPosition)
        {
            return (currentPosition > lastPosition) 
                ? (currentPosition - lastPosition) 
                : (_recordedClip.samples - lastPosition + currentPosition);
        }

        private async Task SendAudioChunk(int startPosition, int length, CancellationToken cancellationToken)
        {
            float[] samples = new float[length];
            _recordedClip.GetData(samples, startPosition);
            byte[] pcmData = ConvertSamplesToPcmBytes(samples);
            
            if (_websocket.State == WebSocketState.Open)
            {
                await _websocket.Send(pcmData);
            }
        }

        private async Task FinishAsrTask(string taskId, CancellationToken cancellationToken)
        {
            var request = new TaskRequest
            {
                header = new Header { action = "finish-task", task_id = taskId }
            };
            
            string json = JsonConvert.SerializeObject(request);
            await _websocket.SendText(json);
            
            // 等待任务完成
            await WaitForCondition(() => _isTaskFinished, 30f, "等待任务完成", cancellationToken);
            Debug.Log("ASR任务完成");
        }

        #endregion

        #region WebSocket消息处理

        private void HandleWebSocketMessage(byte[] bytes)
        {
            try
            {
                var message = System.Text.Encoding.UTF8.GetString(bytes);
                
                #if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[ASR] {message}");
                #endif

                var baseResponse = JsonConvert.DeserializeObject<BaseResponse>(message);
                if (baseResponse?.header == null) return;

                switch (baseResponse.header.@event)
                {
                    case "task-started":
                        _isTaskStarted = true;
                        Debug.Log("ASR任务已启动");
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
                        string errorMessage = baseResponse.header.error_message ?? "ASR任务失败";
                        Debug.LogError($"ASR任务失败: {errorMessage}");
                        UnityMainThreadDispatcher.Instance().Enqueue(() => Error?.Invoke(errorMessage));
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"处理WebSocket消息时出错: {ex.Message}");
            }
        }

        private void HandleRecognitionResult(string message)
        {
            try
            {
                var response = JsonConvert.DeserializeObject<RecognitionResponse>(message);
                string recognizedText = ReconstructTextFromWords(response);
                
                if (!string.IsNullOrEmpty(recognizedText))
                {
                    _lastFinalTranscript = recognizedText;
                    
                    // 在主线程中触发事件
                    UnityMainThreadDispatcher.Instance().Enqueue(() =>
                    {
                        OnRecognitionStreaming?.Invoke(recognizedText);
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"处理识别结果时出错: {ex.Message}");
            }
        }

        private string ReconstructTextFromWords(RecognitionResponse response)
        {
            if (response?.payload?.output?.sentence?.words == null) 
                return "";

            var stringBuilder = new StringBuilder();
            foreach (var word in response.payload.output.sentence.words)
            {
                stringBuilder.Append(word.text); 
                stringBuilder.Append(word.punctuation);
            }
            return stringBuilder.ToString().Trim();
        }

        #endregion

        #region 辅助方法

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
                throw new TimeoutException($"{description}超时");
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
                TimeoutException => "连接超时，请检查网络",
                InvalidOperationException => "设备错误，请重试",
                _ => "处理失败，请重试"
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

        #region Unity生命周期

        private void Update()
        {
            // 处理WebSocket消息队列
            if (_websocket != null && _websocket.State == WebSocketState.Open)
            {
                _websocket.DispatchMessageQueue();
            }
        }

        private void OnDestroy()
        {
            Debug.Log("ASR控制器正在清理...");
            CleanupWebSocket();
            _currentTaskCts?.Cancel();
            _currentTaskCts?.Dispose();
        }

        #endregion
    }

    #region Unity主线程调度器
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