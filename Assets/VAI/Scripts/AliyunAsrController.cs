using UnityEngine;
using UnityEngine.Events;
using NativeWebSocket;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Text;
using System;

// --- 数据结构定义 ---
namespace DashScope.ASR
{
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
        
        // 新增: 对应官方示例的 vocabulary_id
        // 如果为空，则不影响；如果不为空，则会被序列化
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
    public class RecognitionResponse { public ResponseHeader header; public ResponsePayload payload; }
    [System.Serializable]
    public class ResponsePayload { public Output output; }
    [System.Serializable]
    public class Output { public Sentence sentence; }
    
    // 用来匹配 "words" 数组中的每个对象
    [System.Serializable]
    public class Word
    {
        public string text;
        public string punctuation;
    }
    
    [System.Serializable]
    public class Sentence
    {
        public string text; // 这个字段我们不再直接使用，但保留以防万一
        public long begin_time;
        public long end_time;
        public Word[] words;
    }
}

/// <summary>
/// 重构后的ASR控制器 - 移除状态管理，作为纯功能组件
/// </summary>
public class AliyunAsrController : MonoBehaviour
{
    [Header("API 配置")]
    [Tooltip("您的阿里云DashScope API Key")]
    public string ApiKey;
    [Tooltip("（可选）自定义热词词表ID")]
    public string VocabularyId;
    
    public VadWakeWordDetect MicController;

    [Header("Events")]
    [Tooltip("当收到最终识别结果时触发")]
    public UnityEvent<string> OnRecognitionResult;
    [Tooltip("当一次完整的流式识别结束后触发")]
    public UnityEvent OnStreamingFinished;

    // 简化后的属性
    public string LastError { get; private set; } = "";
    public bool IsProcessing { get; private set; } = false;

    // WebSocket相关
    private WebSocket _websocket;
    private string _microphoneDevice;
    private AudioClip _recordedClip;
    
    // 任务控制
    private CancellationTokenSource _currentTaskCts;
    private bool _isTaskStarted;
    private bool _isTaskFinished;
    
    // 识别结果
    private string _lastFinalTranscript = "";

    #region Public API

    /// <summary>
    /// 获取最后的识别结果
    /// </summary>
    public string GetLastFinalTranscript() 
    { 
        return _lastFinalTranscript; 
    }

    /// <summary>
    /// 开始流式识别
    /// </summary>
    public void StartStreaming()
    {
        if (IsProcessing)
        {
            Debug.LogWarning("ASR正在处理中，请勿重复启动");
            return;
        }

        Debug.Log("开始ASR流式识别");
        _ = StartStreamingAsync();
    }

    /// <summary>
    /// 停止流式识别
    /// </summary>
    public void StopStreaming()
    {
        if (!IsProcessing)
        {
            Debug.LogWarning("ASR未在运行，无需停止");
            return;
        }

        Debug.Log("停止ASR流式识别");
        _currentTaskCts?.Cancel();
    }

    /// <summary>
    /// 强制重置ASR状态
    /// </summary>
    public void Reset()
    {
        Debug.Log("重置ASR控制器");
        StopStreaming();
        ClearResults();
    }

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        if (MicController == null)
        {
            Debug.LogError("MicController dependency is not set in AliyunAsrController!");
            return;
        }
        
        Debug.Log("ASR控制器初始化完成");
    }

    private void Update()
    {
        // 只处理WebSocket消息队列
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

    #region Core Recognition Logic

    private async Task StartStreamingAsync()
    {
        try
        {
            IsProcessing = true;
            _currentTaskCts = new CancellationTokenSource();
            var token = _currentTaskCts.Token;

            await ExecuteRecognitionPipeline(token);
        }
        catch (OperationCanceledException)
        {
            Debug.Log("ASR任务被取消");
            LastError = "";
        }
        catch (Exception ex)
        {
            Debug.LogError($"ASR任务失败: {ex.Message}");
            LastError = FormatUserFriendlyError(ex);
        }
        finally
        {
            IsProcessing = false;
            CleanupWebSocket();
            
            // 通知完成（成功或失败）
            UnityMainThreadDispatcher.Instance().Enqueue(() => 
            {
                OnStreamingFinished?.Invoke();
            });
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
        ClearResults();
        
        // 验证麦克风
        _recordedClip = MicController.MicrophoneClip;
        _microphoneDevice = MicController.MicrophoneDevice;
        
        if (_recordedClip == null || !Microphone.IsRecording(_microphoneDevice))
        {
            throw new InvalidOperationException("麦克风设备不可用");
        }
        
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
            LastError = "网络连接失败";
            _isTaskFinished = true; 
        };
        _websocket.OnMessage += HandleWebSocketMessage;
    }

    private async Task StartAsrTask(string taskId, CancellationToken cancellationToken)
    {
        var parameters = new DashScope.ASR.Parameters();
        if (!string.IsNullOrEmpty(VocabularyId))
        {
            parameters.vocabulary_id = VocabularyId;
        }
        
        var request = new DashScope.ASR.TaskRequest
        {
            header = new DashScope.ASR.Header { action = "run-task", task_id = taskId },
            payload = new DashScope.ASR.Payload { parameters = parameters }
        };
        
        string json = JsonUtility.ToJson(request);
        await _websocket.SendText(json);
        
        // 等待任务启动确认
        await WaitForCondition(() => _isTaskStarted, 10f, "等待任务启动", cancellationToken);
        Debug.Log("ASR任务启动成功");
    }

    private async Task StreamAudioData(CancellationToken cancellationToken)
    {
        Debug.Log("开始流式发送音频数据");
        int lastPosition = Microphone.GetPosition(_microphoneDevice);
        const int minChunkSize = 1024; // 最小块大小，避免发送过小的数据块
        
        while (!cancellationToken.IsCancellationRequested)
        {
            int currentPosition = Microphone.GetPosition(_microphoneDevice);
            if (currentPosition != lastPosition)
            {
                int length = CalculateAudioBufferLength(currentPosition, lastPosition);
                
                // 只有当数据块足够大时才发送，提高效率
                if (length >= minChunkSize)
                {
                    await SendAudioChunk(lastPosition, length, cancellationToken);
                    lastPosition = currentPosition;
                }
            }
            
            await Task.Delay(50, cancellationToken); // 平衡响应性和CPU使用率
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
        var request = new DashScope.ASR.TaskRequest
        {
            header = new DashScope.ASR.Header { action = "finish-task", task_id = taskId }
        };
        
        string json = JsonUtility.ToJson(request);
        await _websocket.SendText(json);
        
        // 等待任务完成
        await WaitForCondition(() => _isTaskFinished, 30f, "等待任务完成", cancellationToken);
        Debug.Log("ASR任务完成");
    }

    #endregion

    #region WebSocket Message Handling

            private void HandleWebSocketMessage(byte[] bytes)
        {
            try
            {
                var message = System.Text.Encoding.UTF8.GetString(bytes);
                
                // 性能优化：只在Debug模式下记录完整消息
                #if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[ASR消息] {message}");
                #endif

                var baseResponse = JsonUtility.FromJson<DashScope.ASR.BaseResponse>(message);
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
                        LastError = baseResponse.header.error_message ?? "ASR任务失败";
                        Debug.LogError($"ASR任务失败: {LastError}");
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
            var response = JsonUtility.FromJson<DashScope.ASR.RecognitionResponse>(message);
            string recognizedText = ReconstructTextFromWords(response);
            
            if (!string.IsNullOrEmpty(recognizedText))
            {
                _lastFinalTranscript = recognizedText;
                
                // 在主线程中触发事件
                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                {
                    OnRecognitionResult?.Invoke(recognizedText);
                });
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"处理识别结果时出错: {ex.Message}");
        }
    }

    private string ReconstructTextFromWords(DashScope.ASR.RecognitionResponse response)
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

    #region Helper Methods

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

    private void ClearResults()
    {
        _lastFinalTranscript = "";
        LastError = "";
        _isTaskStarted = false;
        _isTaskFinished = false;
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
}