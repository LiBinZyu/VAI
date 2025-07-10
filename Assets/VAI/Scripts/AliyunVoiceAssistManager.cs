using UnityEngine;
using UnityEngine.UI;
using System.Threading.Tasks;
using VAI;

public class AliyunVoiceAssistManager : MonoBehaviour
{
    [Header("Component References")]
    [Tooltip("The Keyword Recognizer for wake word detection.")]
    public VadWakeWordDetect vadWakeWordDetect;

    [Tooltip("The Aliyun ASR controller for speech-to-text.")]
    public AliyunAsrController asrController;

    [Tooltip("The controller for calling the Large Language Model API.")]
    public apiFuncCalling llmController;

    [Header("UI Feedback")] [Tooltip("A text element to show the current status of the assistant.")]
    public Text VaiResponse;
    
    public Text statusText;
    
    public Animator statusAnimator;

    private enum AssistantState
    {
        Idle,
        ListeningForCommand,
        ProcessingCommand,
        FunctionCalled,
        Invalid
    }
    

    private AssistantState currentState = AssistantState.Idle;
    
    // VAD 状态变量
    private float timeOfLastSound;
    private bool isCheckingForSilence = false;

    void Start()
    {
        if (vadWakeWordDetect == null || asrController == null || llmController == null)
        {
            Debug.LogError("一个或多个组件引用未在Manager中设置！");
            enabled = false;
            return;
        }

        // 订阅事件
        vadWakeWordDetect.OnWakeWordRecognized.AddListener(OnWakeWordDetected);
        asrController.OnStreamingFinished.AddListener(OnAsrStreamingFinished);

        SetState(AssistantState.Idle); // 初始化状态
    }

    void Update()
    {
        // VAD静音检测逻辑保持不变
        if (isCheckingForSilence && currentState == AssistantState.ListeningForCommand)
        {
            float currentVolume = vadWakeWordDetect.GetMicrophoneVolume();
            if (currentVolume > vadWakeWordDetect.volumeThreshold)
            {
                timeOfLastSound = Time.time;
            }
            else
            {
                if (Time.time - timeOfLastSound > vadWakeWordDetect.silenceThreshold)
                {
                    Debug.Log($"检测到持续静音超过 {vadWakeWordDetect.silenceThreshold} 秒。正在停止ASR...");
                    isCheckingForSilence = false; 
                    asrController.StopStreaming();
                }
            }
        }
    }

    private void OnDestroy()
    {
        if (vadWakeWordDetect != null) vadWakeWordDetect.OnWakeWordRecognized.RemoveListener(OnWakeWordDetected);
        if (asrController != null) asrController.OnStreamingFinished.RemoveListener(OnAsrStreamingFinished);
    }

    private void SetState(AssistantState newState)
    {
        currentState = newState;
        UpdateUIForState(); // 集中更新UI

        // 根据新状态决定是否开启VAD静音检测
        isCheckingForSilence = (newState == AssistantState.ListeningForCommand);
        if (isCheckingForSilence)
        {
            // 当开始聆听时，启动VAD检测，并给予短暂缓冲
            timeOfLastSound = Time.time + vadWakeWordDetect.listeningDelay; 
        }
    }

    // 新增：集中处理UI更新的方法
    private void UpdateUIForState()
    {
        UpdateStateAnimation(); // 触发动画状态

        switch (currentState)
        {
            case AssistantState.Idle:
                if (statusText != null) statusText.text = "";
                break;
            case AssistantState.ListeningForCommand:
                if (statusText != null) statusText.text = "倾听中...";
                break;
            case AssistantState.ProcessingCommand:
                if (statusText != null) statusText.text = "思考中...";
                break;
            case AssistantState.FunctionCalled:
                if (statusText != null) statusText.text = "指令已执行";
                break;
            case AssistantState.Invalid:
                if (statusText != null) statusText.text = "发生一些错误，请重试";
                break;
        }
    }
    
    private void UpdateStateAnimation()
    {
        if (statusAnimator == null) return;
        statusAnimator.SetTrigger("assist_"+ currentState);
    }

    // --- 事件处理器 ---

    // 1. 被 VadWakeWordDetect 调用
    public void OnWakeWordDetected()
    {
        if (currentState != AssistantState.Idle)
        {
            Debug.LogWarning("系统正忙，忽略本次唤醒。");
            return;
        }

        SetState(AssistantState.ListeningForCommand);
        asrController.StartStreaming();
    }

    // 2. 被 AliyunAsrController 在流结束后调用
    public async void OnAsrStreamingFinished()
    {
        if (currentState != AssistantState.ListeningForCommand)
        {
            Debug.LogWarning("ASR在非聆听状态下结束，可能是意外情况，直接重置系统。");
            SetState(AssistantState.Idle);
            return;
        }

        // 检查是否有网络错误
        if (!string.IsNullOrEmpty(asrController.LastError))
        {
            Debug.LogWarning($"ASR发生网络错误: {asrController.LastError}");
            SetState(AssistantState.Invalid);
            // 在设置状态后再覆盖statusText，确保网络错误信息能正确显示
            if (statusText != null) statusText.text = asrController.LastError;
            await Task.Delay(2500);
            SetState(AssistantState.Idle);
            return;
        }

        try
        {
            SetState(AssistantState.ProcessingCommand);

            string command = asrController.GetLastFinalTranscript();
            print("ASR完成，识别结果为: " + command);

            if (string.IsNullOrWhiteSpace(command))
            {
                Debug.LogWarning("ASR完成，但未识别到有效指令。");
                if (statusText != null) statusText.text = "抱歉，没听清。请重试。";
                await Task.Delay(1500);
                // 此处无需操作，finally代码块会负责重置状态
                return;
            }

            if (statusText != null)
            {
                statusText.text = $"已识别: \"{command}\"。正在处理...";
            }

            // 3. 触发LLM处理
            bool hasFunctionCall = await llmController.ProcessCommand(command);
            
            // 4. 根据是否有函数调用决定状态
            if (hasFunctionCall)
            {
                OnFunctionCalled();
            }
            else
            {
                OnInvalidResponse();
            }
        }
        catch (System.Exception ex)
        {
            // 捕获处理过程中可能发生的任何异常
            Debug.LogError($"指令处理过程中发生错误: {ex.Message}");
            if (statusText != null) statusText.text = "处理指令时出错。";
            await Task.Delay(1500);
            SetState(AssistantState.Idle);
        }
    }
    
    // This method should be called by your LLM/API script after a function executes
    public async void OnFunctionCalled() 
    {
        SetState(AssistantState.FunctionCalled);
        await Task.Delay(2500);
        SetState(AssistantState.Idle);
    }
    
    // This method should be called when LLM responds without function calling
    public async void OnInvalidResponse() 
    {
        SetState(AssistantState.Invalid);
        await Task.Delay(2500);
        SetState(AssistantState.Idle);
    }
}