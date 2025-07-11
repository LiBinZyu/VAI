using UnityEngine;
using UnityEngine.UI;
using System.Threading.Tasks;
using System.Threading;
using System;
using VAI;

namespace VAI{
    public class VAItoggle{
        public void StartVAI(){

        }
    }   

}
public class AliyunVoiceAssistManager : MonoBehaviour
{
    [Header("Component References")]
    [Tooltip("The Keyword Recognizer for wake word detection.")]
    public VadWakeWordDetect vadWakeWordDetect;

    [Tooltip("The Aliyun ASR controller for speech-to-text.")]
    public AliyunAsrController asrController;

    [Tooltip("The controller for calling the Large Language Model API.")]
    public apiFuncCalling llmController;

    [Header("UI Feedback")] 
    [Tooltip("A text element to show the current status of the assistant.")]
    public Text VaiResponse;
    
    public Text statusText;
    public Animator statusAnimator;

    [Header("Timeout Settings")]
    [Tooltip("Maximum time to wait for ASR response (seconds)")]
    public float asrTimeoutSeconds = 10f;
    
    [Tooltip("Maximum time to wait for LLM response (seconds)")]
    public float llmTimeoutSeconds = 30f;
    
    [Tooltip("Time to show status messages before returning to idle")]
    public float statusDisplayTime = 2.5f;
    
    [Tooltip("Maximum time to stay in any non-idle state before auto-recovery (seconds)")]
    public float maxStateTimeSeconds = 60f;

    // 状态机枚举
    public enum VoiceAssistantState
    {
        Idle,
        ListeningForCommand,    // 已经被唤醒后持续ASR的状态
        ProcessingCommand,
        FunctionCalled,        // 函数执行成功后激活的状态，维持2.5秒
        Invalid,
        Shutdown
    }

    // 状态机
    private VoiceAssistantState _currentState = VoiceAssistantState.Idle;
    private CancellationTokenSource _mainCancellationTokenSource;
    private CancellationTokenSource _currentTaskCancellationTokenSource;
    
    // VAD 相关
    private float _timeOfLastSound;
    private bool _isMonitoringForSilence;
    
    // 任务管理
    private Task _currentTask;
    private bool _isInitialized = false;
    
    // 看门狗机制
    private float _stateChangeTime;
    private bool _watchdogEnabled = true;

    #region Unity Lifecycle

    void Start()
    {
        InitializeAsync().ConfigureAwait(false);
    }

    void Update()
    {
        // 只处理VAD静音检测，其他逻辑移到状态机中
        HandleVadSilenceDetection();
        
        // 看门狗检查，防止系统卡死
        HandleWatchdogCheck();
    }

    void OnDestroy()
    {
        TransitionToState(VoiceAssistantState.Shutdown);
        Cleanup();
    }

    #endregion

    #region Initialization and Cleanup

    private async Task InitializeAsync()
    {
        try
        {
            // 验证组件引用
            if (vadWakeWordDetect == null || asrController == null || llmController == null)
            {
                Debug.LogError("一个或多个组件引用未在Manager中设置！");
                TransitionToState(VoiceAssistantState.Invalid);
                return;
            }

            // 创建主取消令牌
            _mainCancellationTokenSource = new CancellationTokenSource();

            // 订阅事件
            vadWakeWordDetect.OnWakeWordRecognized.AddListener(OnWakeWordDetected);
            asrController.OnStreamingFinished.AddListener(OnAsrStreamingFinished);

            _isInitialized = true;
            TransitionToState(VoiceAssistantState.Idle);

            Debug.Log("语音助手管理器初始化完成");
        }
        catch (Exception ex)
        {
            Debug.LogError($"初始化失败: {ex.Message}");
            TransitionToState(VoiceAssistantState.Invalid);
        }
    }

    private void Cleanup()
    {
        try
        {
            // 取消所有进行中的任务
            _mainCancellationTokenSource?.Cancel();
            _currentTaskCancellationTokenSource?.Cancel();

            // 等待当前任务完成（最多1秒）
            if (_currentTask != null && !_currentTask.IsCompleted)
            {
                _currentTask.Wait(TimeSpan.FromSeconds(1));
            }

            // 清理事件订阅
            if (vadWakeWordDetect != null) 
                vadWakeWordDetect.OnWakeWordRecognized.RemoveListener(OnWakeWordDetected);
            if (asrController != null) 
                asrController.OnStreamingFinished.RemoveListener(OnAsrStreamingFinished);

            // 释放资源
            _mainCancellationTokenSource?.Dispose();
            _currentTaskCancellationTokenSource?.Dispose();

            Debug.Log("语音助手管理器清理完成");
        }
        catch (Exception ex)
        {
            Debug.LogError($"清理过程中发生错误: {ex.Message}");
        }
    }

    #endregion

    #region State Machine

    /// <summary>
    /// 线程安全的状态转换方法
    /// </summary>
    private void TransitionToState(VoiceAssistantState newState)
    {
        if (_currentState == newState) return;

        Debug.Log($"状态转换: {_currentState} -> {newState}");
        
        var oldState = _currentState;
        _currentState = newState;
        _stateChangeTime = Time.time; // 更新状态变更时间
        
        // 在主线程中更新UI
        if (Application.isPlaying)
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() => UpdateUIForState(newState));
        }

        // 处理状态退出逻辑
        HandleStateExit(oldState);
        
        // 处理状态进入逻辑
        HandleStateEntry(newState);
    }

    private void HandleStateExit(VoiceAssistantState exitingState)
    {
        switch (exitingState)
        {
            case VoiceAssistantState.ListeningForCommand:
                _isMonitoringForSilence = false;
                break;
        }
    }

    private void HandleStateEntry(VoiceAssistantState enteringState)
    {
        switch (enteringState)
        {
            case VoiceAssistantState.Idle:
                // 确保可以接受唤醒词
                break;
                
            case VoiceAssistantState.ListeningForCommand:
                StartVadSilenceMonitoring();
                break;
                
            case VoiceAssistantState.Invalid:
                // 自动恢复机制
                ScheduleErrorRecovery();
                break;
                
            case VoiceAssistantState.Shutdown:
                // 停止所有活动
                break;
        }
    }

    private void UpdateUIForState(VoiceAssistantState state)
    {
        // 更新动画
        if (statusAnimator != null)
        {
            statusAnimator.SetTrigger("assist_" + state.ToString());
        }

        // 更新状态文本
        if (statusText != null)
        {
            statusText.text = GetStatusTextForState(state);
        }
    }

    private string GetStatusTextForState(VoiceAssistantState state)
    {
        switch (state)
        {
            case VoiceAssistantState.Idle:
                return "";
            case VoiceAssistantState.ListeningForCommand:
                return "倾听中...";
            case VoiceAssistantState.ProcessingCommand:
                return "思考中...";
            case VoiceAssistantState.FunctionCalled:
                return "指令已执行";
            case VoiceAssistantState.Invalid:
                return "发生错误，正在重试...";
            default:
                return state.ToString();
        }
    }

    #endregion

    #region VAD Silence Detection

    private void StartVadSilenceMonitoring()
    {
        _timeOfLastSound = Time.time + vadWakeWordDetect.listeningDelay;
        _isMonitoringForSilence = true;
    }

    private void HandleVadSilenceDetection()
    {
        if (!_isMonitoringForSilence || _currentState != VoiceAssistantState.ListeningForCommand)
            return;

        float currentVolume = vadWakeWordDetect.GetMicrophoneVolume();
        if (currentVolume > vadWakeWordDetect.volumeThreshold)
        {
            _timeOfLastSound = Time.time;
        }
        else if (Time.time - _timeOfLastSound > vadWakeWordDetect.silenceThreshold)
        {
            Debug.Log($"检测到持续静音超过 {vadWakeWordDetect.silenceThreshold} 秒，停止ASR");
            _isMonitoringForSilence = false;
            asrController.StopStreaming();
        }
    }

    private void HandleWatchdogCheck()
    {
        if (!_watchdogEnabled || !_isInitialized) return;

        // 检查是否在非空闲状态停留过久
        if (_currentState != VoiceAssistantState.Idle && 
            _currentState != VoiceAssistantState.Idle)
        {
            if (Time.time - _stateChangeTime > maxStateTimeSeconds)
            {
                Debug.LogWarning($"状态 {_currentState} 停留超过 {maxStateTimeSeconds} 秒，执行强制重置");
                ForceReset();
            }
        }
    }

    #endregion

    #region Event Handlers

    public void OnWakeWordDetected()
    {
        if (_currentState != VoiceAssistantState.Idle)
        {
            Debug.LogWarning($"在 {_currentState} 状态下收到唤醒词，忽略");
            return;
        }

        Debug.Log("检测到唤醒词");
        TransitionToState(VoiceAssistantState.ListeningForCommand);
        
        // 启动ASR任务
        StartNewTask(StartAsrListeningTask);
    }

    public void OnAsrStreamingFinished()
    {
        if (_currentState != VoiceAssistantState.ListeningForCommand)
        {
            Debug.LogWarning($"在 {_currentState} 状态下收到ASR完成信号");
            TransitionToState(VoiceAssistantState.Idle);
            return;
        }

        Debug.Log("ASR流结束，开始处理结果");
        
        // 启动命令处理任务
        StartNewTask(ProcessAsrResultTask);
    }

    #endregion

    #region Task Management

    private void StartNewTask(Func<CancellationToken, Task> taskFactory)
    {
        // 取消之前的任务
        _currentTaskCancellationTokenSource?.Cancel();
        _currentTaskCancellationTokenSource?.Dispose();
        
        // 创建新的取消令牌
        _currentTaskCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
            _mainCancellationTokenSource.Token);
        
        // 启动新任务
        _currentTask = taskFactory(_currentTaskCancellationTokenSource.Token);
        
        // 处理任务异常
        _currentTask.ContinueWith(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogError($"任务执行失败: {task.Exception?.GetBaseException().Message}");
                UnityMainThreadDispatcher.Instance().Enqueue(() => 
                    TransitionToState(VoiceAssistantState.Invalid));
            }
        }, TaskScheduler.Default);
    }

    private async Task StartAsrListeningTask(CancellationToken cancellationToken)
    {
        try
        {
            asrController.StartStreaming();
            
            // 简单等待，让ASR自己处理超时
            // ASR完成时会触发OnAsrStreamingFinished事件
            Debug.Log("ASR任务已启动，等待完成");
        }
        catch (OperationCanceledException)
        {
            Debug.Log("ASR任务被取消");
            asrController.StopStreaming();
        }
        catch (Exception ex)
        {
            Debug.LogError($"ASR任务失败: {ex.Message}");
            asrController.StopStreaming();
            throw;
        }
    }

    private async Task ProcessAsrResultTask(CancellationToken cancellationToken)
    {
        try
        {
            TransitionToState(VoiceAssistantState.ProcessingCommand);

            // 检查ASR错误
            if (!string.IsNullOrEmpty(asrController.LastError))
            {
                Debug.LogWarning($"ASR错误: {asrController.LastError}");
                await ShowErrorMessage(asrController.LastError);
                return;
            }

            // 获取识别结果
            string command = asrController.GetLastFinalTranscript();
            Debug.Log($"ASR识别结果: {command}");

            if (string.IsNullOrWhiteSpace(command))
            {
                await ShowErrorMessage("抱歉，没听清。请重试。");
                return;
            }

            // 更新UI显示识别结果
            await UnityMainThreadDispatcher.Instance().EnqueueAsync(() =>
            {
                if (statusText != null)
                    statusText.text = $"已识别: \"{command}\"。正在处理...";
            });

            // 处理LLM命令
            await ProcessLlmCommand(command, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            Debug.Log("命令处理任务被取消");
        }
        catch (Exception ex)
        {
            Debug.LogError($"命令处理失败: {ex.Message}");
            await ShowErrorMessage("处理指令时出错");
        }
        finally
        {
            // 确保最终返回到等待状态
            await Task.Delay(100); // 短暂延迟确保UI更新
            TransitionToState(VoiceAssistantState.Idle);
        }
    }

    private async Task ProcessLlmCommand(string command, CancellationToken cancellationToken)
    {
        try
        {
            // 使用超时机制调用LLM
            var llmTask = llmController.ProcessCommand(command);
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(llmTimeoutSeconds), cancellationToken);
            
            var completedTask = await Task.WhenAny(llmTask, timeoutTask);
            
            if (completedTask == timeoutTask)
            {
                throw new TimeoutException("LLM响应超时");
            }
            
            bool hasToolCall = await llmTask;
            
            if (hasToolCall)
            {
                // 函数执行成功后激活FunctionCalled状态，维持2.5秒
                TransitionToState(VoiceAssistantState.FunctionCalled);
                await Task.Delay(TimeSpan.FromSeconds(statusDisplayTime), cancellationToken);
            }
            
            Debug.Log($"命令处理完成，工具调用: {hasToolCall}");
        }
        catch (TimeoutException ex)
        {
            Debug.LogError($"LLM处理超时: {ex.Message}");
            await ShowErrorMessage("处理超时，请重试");
        }
        catch (Exception ex)
        {
            Debug.LogError($"LLM处理失败: {ex.Message}");
            await ShowErrorMessage("处理失败，请重试");
        }
    }

    private async Task ShowErrorMessage(string message)
    {
        TransitionToState(VoiceAssistantState.Invalid);
        
        await UnityMainThreadDispatcher.Instance().EnqueueAsync(() =>
        {
            if (statusText != null) statusText.text = message;
            if (VaiResponse != null) VaiResponse.text = message;
        });
        
        await Task.Delay(TimeSpan.FromSeconds(statusDisplayTime));
    }

    private void ScheduleErrorRecovery()
    {
        // 错误状态自动恢复
        StartNewTask(async (cancellationToken) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(statusDisplayTime), cancellationToken);
            TransitionToState(VoiceAssistantState.Idle);
        });
    }

    #endregion

    #region Public API for external components

    /// <summary>
    /// 强制重置到等待状态（紧急情况使用）
    /// </summary>
    public void ForceReset()
    {
        Debug.LogWarning("执行强制重置");
        _currentTaskCancellationTokenSource?.Cancel();
        TransitionToState(VoiceAssistantState.Idle);
    }

    /// <summary>
    /// 获取当前状态
    /// </summary>
    public VoiceAssistantState GetCurrentState()
    {
        return _currentState;
    }

    #endregion
}

/// <summary>
/// Unity主线程调度器 - 用于从其他线程安全地更新Unity组件
/// </summary>
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