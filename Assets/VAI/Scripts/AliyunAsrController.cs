using UnityEngine;
using UnityEngine.Events;
using NativeWebSocket;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Text;

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
    [Tooltip("当一次完整的流式识别（包括等待服务器最终响应）结束后触发")]
    public UnityEvent OnStreamingFinished; // 新增的事件

    public enum RecognizerState { Idle, Ready, Recording, Processing, Error }
    public RecognizerState CurrentState { get; private set; } = RecognizerState.Idle;

    private WebSocket websocket;
    private string microphoneDevice;
    private AudioClip recordedClip;
    private bool isTaskStarted;
    private bool isTaskFinished;
    
    // 保存最终结果给大模型
    private Dictionary<long, string> _recognizedSentences = new Dictionary<long, string>();
    private string _lastFinalTranscript = "";
    public string GetLastFinalTranscript() { return _lastFinalTranscript; }
    
    // 为每次识别任务创建一个独立的CancellationTokenSource，避免互相干扰
    private CancellationTokenSource recognitionTaskCts;

    #region MonoBehaviour 生命周期

    private void Start()
    {
        if (MicController == null)
        {
            Debug.LogError("MicController dependency is not set in AliyunAsrController!");
            CurrentState = RecognizerState.Error;
            return;
        }
        CurrentState = RecognizerState.Ready;
    }

    private void Update()
    {
        if (websocket != null && websocket.State == WebSocketState.Open)
        {
            websocket.DispatchMessageQueue();
        }
    }

    private void OnDestroy()
    {
        Debug.Log("[生命周期] OnDestroy 被调用。如果此时正在处理任务，任务将被取消。");
        // 取消正在进行的任务
        recognitionTaskCts?.Cancel();
        recognitionTaskCts?.Dispose();

        // 安全关闭连接
        if (websocket != null && websocket.State == WebSocketState.Open)
        {
            websocket.Close();
        }
    }

    #endregion

    #region 公共控制方法

    public void StartStreaming()
    {
        if (CurrentState == RecognizerState.Processing || CurrentState == RecognizerState.Recording)
        {
            Debug.LogWarning("当前正在识别中，请勿重复启动。");
            return;
        }

        // 启动一个全新的、独立的流式识别任务
        _ = StreamingRecognitionTask();
    }

// 提供一个手动停止的方法，以防VAD不生效或需要强制中断
    public void StopStreaming()
    {
        if (recognitionTaskCts != null && !recognitionTaskCts.IsCancellationRequested)
        {
            Debug.Log("手动停止流式识别任务...");
            recognitionTaskCts.Cancel();
        }
    }

    #endregion

    #region 核心识别流程

    private async Task StreamingRecognitionTask()
    {
        // 1. 初始化状态和控制令牌
        CurrentState = RecognizerState.Processing;
        recognitionTaskCts = new CancellationTokenSource();
        var token = recognitionTaskCts.Token;

        ResetTaskStatus();
        //string finalRecognizedText = "";

        // 使用麦克风
        AudioClip recordedClip = MicController.MicrophoneClip;
        string microphoneDevice = MicController.MicrophoneDevice;
        if (recordedClip == null || !Microphone.IsRecording(microphoneDevice))
        {
            Debug.LogError("没有拿到可用麦克风从MicController");
            CurrentState = RecognizerState.Error;
            return;
        }
        Debug.Log("ASR task 使用麦克风");

        // 3. 配置WebSocket
        string url = "wss://dashscope.aliyuncs.com/api-ws/v1/inference/";
        var headers = new Dictionary<string, string> { { "Authorization", $"Bearer {ApiKey}" }, { "X-DashScope-DataInspection", "enable" } };
        websocket = new WebSocket(url, headers);

        // 4. 配置事件回调 (这部分不变)
        websocket.OnOpen += () => Debug.Log("WebSocket 连接已打开。");
        websocket.OnClose += (e) => Debug.Log($"WebSocket 连接已关闭，代码: {e}");
        websocket.OnError += (e) => { Debug.LogError($"WebSocket 错误: {e}"); isTaskFinished = true; };
        websocket.OnMessage += (bytes) => HandleMessage(bytes);

        try
        {
            websocket.Connect();

            float connectTimeout = 10f; // 10秒连接超时
            Debug.Log("正在尝试连接 WebSocket...");
            while (websocket.State != WebSocketState.Open && connectTimeout > 0 && !token.IsCancellationRequested)
            {
                await Task.Delay(100, token);
                connectTimeout -= 0.1f;
            }

            // 检查连接是否成功
            if (websocket.State != WebSocketState.Open)
            {
                if (token.IsCancellationRequested)
                {
                     Debug.LogWarning("连接过程中任务被取消。");
                }
                else
                {
                     Debug.LogError($"WebSocket 连接超时或失败，当前状态: {websocket.State}");
                }
                throw new System.Exception("WebSocket 连接未成功建立。");
            }
            
            Debug.Log("WebSocket 连接已确认，继续执行任务。");

            // 6. 连接成功后，继续执行后续指令
            string taskId = System.Guid.NewGuid().ToString("N");
            await SendRunTaskCommand(taskId);
            await WaitForTaskStart(token, () => isTaskStarted);

            // 7. --- 核心循环：捕获、转换、发送 ---
            Debug.Log("进入实时音频流发送循环...");
            CurrentState = RecognizerState.Recording;
            int lastPosition = Microphone.GetPosition(microphoneDevice);
            
            while (!token.IsCancellationRequested)
            {
                int currentPosition = Microphone.GetPosition(microphoneDevice);
                if (currentPosition != lastPosition)
                {
                    int length = (currentPosition > lastPosition) ? (currentPosition - lastPosition) : (recordedClip.samples - lastPosition + currentPosition);
                    
                    if (length > 0)
                    {
                        float[] samples = new float[length];
                        recordedClip.GetData(samples, lastPosition);
                        lastPosition = currentPosition;
                        byte[] pcmData = ConvertSamplesToPcmBytes(samples);
                        if (websocket.State == WebSocketState.Open)
                        {
                            await websocket.Send(pcmData);
                        }
                    }
                }
                // 等待一小段时间，避免CPU空转
                await Task.Delay(100, token);
            }
            
            Debug.Log("音频发送循环已终止。");
            
            await SendFinishTaskCommand(taskId);
            await WaitForTaskFinish(token, () => isTaskFinished);
        }
        catch (System.OperationCanceledException)
        {
            Debug.Log("识别任务被正常取消。");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"识别流程出现异常: {ex.Message}");
        }
        finally
        {
            // [关键修复] 无论任务如何结束，都首先通知Manager
            Debug.Log("任务流程结束，正在通知Manager...");
            OnStreamingFinished?.Invoke(); 

            if (websocket != null && websocket.State == WebSocketState.Open) await websocket.Close();
            CurrentState = RecognizerState.Ready;
            recognitionTaskCts.Dispose();
            recognitionTaskCts = null;
        }
    }
    private void HandleMessage(byte[] bytes)
    {
        var message = System.Text.Encoding.UTF8.GetString(bytes);
        Debug.Log($"[服务器消息] {message}"); // 保留原始消息日志

        var baseResponse = JsonUtility.FromJson<DashScope.ASR.BaseResponse>(message);
        if (baseResponse?.header == null) return;

        // [核心修改] 将拼接逻辑提取到一个单独的方法中，并应用于 result-generated 和 task-finished
        string reconstructedText = "";

        switch (baseResponse.header.@event)
        {
            case "task-started":
                isTaskStarted = true;
                break;

            case "result-generated":
                var intermediateResponse = JsonUtility.FromJson<DashScope.ASR.RecognitionResponse>(message);
                reconstructedText = ReconstructTextFromWords(intermediateResponse);
                if (!string.IsNullOrEmpty(reconstructedText))
                {
                    Debug.Log($"[中间结果拼接] {reconstructedText}");
                    _lastFinalTranscript = reconstructedText;// 保存最终结果给大模型
                    OnRecognitionResult?.Invoke(reconstructedText); // 对中间结果触发事件
                }
                break;

            case "task-finished":
                isTaskFinished = true;
                var finalResponse = JsonUtility.FromJson<DashScope.ASR.RecognitionResponse>(message);
                reconstructedText = ReconstructTextFromWords(finalResponse);
                if (!string.IsNullOrEmpty(reconstructedText))
                {
                    Debug.Log($"[最终结果拼接] {reconstructedText}");
                    // 这里可以根据需求决定是否再次触发 OnRecognitionResult
                    // 通常，最终结果在 OnStreamingFinished 之前最后一次更新即可
                    OnRecognitionResult?.Invoke(reconstructedText);
                }
                break;

            case "task-failed":
                isTaskFinished = true;
                Debug.LogError($"任务失败: {baseResponse.header.error_message}");
                break;
        }
    }
    
    #endregion

    #region 辅助方法

    private string ReconstructTextFromWords(DashScope.ASR.RecognitionResponse response)
    {
        var stringBuilder = new StringBuilder();
        foreach (var word in response.payload.output.sentence.words)
        {
            stringBuilder.Append(word.text); 
            stringBuilder.Append(word.punctuation);
        }
        return stringBuilder.ToString();
    }
    
    public static byte[] ConvertSamplesToPcmBytes(float[] samples)
    {
        // 直接在内存中创建字节数组
        byte[] pcmData = new byte[samples.Length * 2]; // 16-bit = 2 bytes per sample
        int byteIndex = 0;

        foreach (var sample in samples)
        {
            // 将-1.0f到1.0f的浮点样本转换为16-bit的short整数
            short intSample = (short)(sample * short.MaxValue);
            // 手动将 short 转换为两个字节 (Little Endian)
            byte[] sampleBytes = System.BitConverter.GetBytes(intSample);
            pcmData[byteIndex++] = sampleBytes[0];
            pcmData[byteIndex++] = sampleBytes[1];
        }
        return pcmData;
    }
    
    private Task SendRunTaskCommand(string taskId)
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
        Debug.Log($"发送 'run-task' 指令: {json}");
        return websocket.SendText(json);
    }
    
    private Task SendFinishTaskCommand(string taskId)
    {
        var request = new DashScope.ASR.TaskRequest
        {
            header = new DashScope.ASR.Header { action = "finish-task", task_id = taskId }
        };
        string json = JsonUtility.ToJson(request);
        Debug.Log($"发送 'finish-task' 指令: {json}");
        return websocket.SendText(json);
    }

    // 新增：分块发送音频数据
    private async Task SendAudioInChunks(byte[] audioData, CancellationToken token)
    {
        Debug.Log($"开始分块发送总计 {audioData.Length} 字节的音频数据...");
        int chunkSize = 1024 * 2; // 每次发送的块大小 (可调整)
        int offset = 0;
        while (offset < audioData.Length && !token.IsCancellationRequested)
        {
            int remaining = audioData.Length - offset;
            int currentChunkSize = Mathf.Min(chunkSize, remaining);
            byte[] chunkData = new byte[currentChunkSize];
            System.Array.Copy(audioData, offset, chunkData, 0, currentChunkSize);
            await websocket.Send(chunkData);
            
            offset += currentChunkSize;
            Debug.Log($"已发送 {offset}/{audioData.Length} 字节...");
            
            // 模拟实时流，延迟100ms
            await Task.Delay(100, token);
        }
        Debug.Log("音频数据发送完毕。");
    }
    
    // 新增：等待任务开始的辅助方法
    private async Task WaitForTaskStart(CancellationToken token, System.Func<bool> condition)
    {
        Debug.Log("等待服务器响应 'task-started'...");
        float timeout = 10f; // 10秒超时
        while (!condition() && timeout > 0)
        {
            await Task.Delay(100, token);
            timeout -= 0.1f;
        }
        if (!condition())
        {
            throw new TaskCanceledException("等待 'task-started' 超时。请检查API Key和网络。");
        }
        Debug.Log("'task-started' 已收到。");
    }

    // 新增：等待任务结束的辅助方法
    private async Task WaitForTaskFinish(CancellationToken token, System.Func<bool> condition)
    {
        Debug.Log("等待服务器响应 'task-finished' 或 'task-failed'...");
        float timeout = 60f; // 60秒超时
        while (!condition() && timeout > 0)
        {
            await Task.Delay(100, token);
            timeout -= 0.1f;
        }
        if (!condition())
        {
            Debug.LogWarning("等待任务结束超时。");
        } 
        Debug.Log("任务结束事件已收到。");
    }
    
    private void ResetTaskStatus()
    {
        isTaskStarted = false;
        isTaskFinished = false;
        _lastFinalTranscript = "";
        _recognizedSentences.Clear();
    }

    #endregion
}