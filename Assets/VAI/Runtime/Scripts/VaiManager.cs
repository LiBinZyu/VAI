using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Windows.Speech;

namespace VAI
{
    public class VAIManager : MonoBehaviour
    {
        public enum AssistantState { Idle, Listening, Processing, Success, Invalid, Shutdown }
        private class VaiConversationData
        {
            public string WakeWord { get; set; }
            public float WakeWordConfidence { get; set; }
            public string AsrResult { get; set; }
            public string LlmResult { get; set; }
            public string ErrorInfo { get; set; }

            public void Clear()
            {
                WakeWord = null;
                WakeWordConfidence = 0;
                AsrResult = null;
                LlmResult = null;
                ErrorInfo = null;
            }

            public void ClearMainInfo()
            {
                AsrResult = null;
                LlmResult = null;
                ErrorInfo = null;
            }
        }

        [Header("Module References")]
        [SerializeField] private VadWakeWordDetect vadModule;
        [SerializeField] private AsrController asrModule;
        [SerializeField] private LlmController llmModule;

        [Header("UI References")]
        [SerializeField] private Text statusText;
        [SerializeField] private Animator statusAnimator;
        [Tooltip("How long to display the Success/Invalid state before returning to Idle (in seconds).")]
        [SerializeField] private float resultDisplayTime = 4.0f;
        
        private AssistantState _currentState;
        private VaiConversationData _conversationData;
        private Color statusTextColor;

        #region Startup and Shutdown

        public void Startup()
        {
            Debug.Log("VAI Manager: Starting up...");
            _conversationData = new VaiConversationData();
            // Subscribe to module events
            vadModule.OnWakeWordRecognized += HandleWakeWordRecognized;
            vadModule.OnSilenceDetected += HandleSilenceDetected;
            asrModule.OnRecognitionStreaming += HandleAsrRecognitionStreaming;
            asrModule.Error += HandleAsrError;
            llmModule.OnProcessingComplete += HandleLlmProcessingComplete;
            llmModule.Error += HandleLlmError;

            vadModule.Initialize();

            statusTextColor = statusText.color;

            // Start in the Idle state
            TransitionToState(AssistantState.Idle);
            Debug.Log("VAI Manager: Startup complete. Listening for wake word.");
        }

        public void Shutdown()
        {
            Debug.Log("VAI Manager: Shutting down...");
            TransitionToState(AssistantState.Shutdown);
            // Unsubscribe to prevent memory leaks
            vadModule.OnWakeWordRecognized -= HandleWakeWordRecognized;
            vadModule.OnSilenceDetected -= HandleSilenceDetected;
            asrModule.OnRecognitionStreaming -= HandleAsrRecognitionStreaming;
            asrModule.Error -= HandleAsrError;
            llmModule.OnProcessingComplete -= HandleLlmProcessingComplete;
            llmModule.Error -= HandleLlmError;
            
            // Ensure all modules are properly shut down
            vadModule.Shutdown();
            asrModule.StopRecognition();
            llmModule.CancelProcessing();

            // Go to a final "off" state
            if (statusText != null) statusText.text = "VAI Inactive";
            Debug.Log("VAI Manager: Shutdown complete.");
        }

        private void OnDestroy()
        {
            // Final safety net to ensure everything is cleaned up.
            Shutdown();
        }

        void Start()
        {
            Startup();
        }

        #endregion

        #region State Machine

        private void TransitionToState(AssistantState newState)
        {
            if (_currentState == newState) return;
            _currentState = newState;

            statusAnimator?.SetTrigger(newState.ToString());

            // Handle state entry logic
            switch (newState)
            {
                case AssistantState.Idle:
                    // Clear conversation log for the next interaction
                    _conversationData.Clear();
                    UpdateStatusText();
                    vadModule.StopSilenceMonitoring();
                    asrModule.StopRecognition();
                    llmModule.CancelProcessing();
                    break;
                case AssistantState.Listening:
                    // Cancel any pending return to idle (in case we're interrupting Success/Invalid states)
                    CancelInvoke(nameof(ReturnToIdle));
                    _conversationData.ClearMainInfo();
                    asrModule.StartRecognition();
                    vadModule.StartSilenceMonitoring();
                    break;
                case AssistantState.Processing:
                    asrModule.StopRecognition();
                    llmModule.ProcessCommand(_conversationData.AsrResult);
                    break;
                case AssistantState.Success:
                    Invoke(nameof(ReturnToIdle), resultDisplayTime);
                    break;
                case AssistantState.Invalid:
                    // After a delay, return to idle
                    Invoke(nameof(ReturnToIdle), resultDisplayTime);
                    break;
                default:
                    break;
            }
        }
        
        private void ReturnToIdle()
        {
            TransitionToState(AssistantState.Idle);
        }

        #endregion

        #region Event Handlers

        private void HandleWakeWordRecognized(PhraseRecognizedEventArgs args)
        {
            // Only respond to wake words when idle, or when in Success/Invalid states (to allow interruption)
            if (_currentState != AssistantState.Idle && 
                _currentState != AssistantState.Success && 
                _currentState != AssistantState.Invalid) return;
            
            _conversationData.WakeWord = args.text;
            _conversationData.WakeWordConfidence = 
            args.confidence switch
            {
                ConfidenceLevel.Low => 0.3f,
                ConfidenceLevel.Medium => 0.6f,
                ConfidenceLevel.High => 0.9f,
                _ => 0.1f
            };
            TransitionToState(AssistantState.Listening);
            UpdateStatusText();
        }

        private void HandleSilenceDetected(bool isDuringWakeBufferTime)
        {
            if (_currentState != AssistantState.Listening) return;

            if (isDuringWakeBufferTime)
            {
                // No volume change detected during wake buffer time, return to idle
                Debug.Log("MANAGER: No volume change detected during wake buffer time, returning to idle.");
                TransitionToState(AssistantState.Idle);
            }
            else
            {
                // Silence tells us the user has finished speaking. Stop the ASR process.
                // The ASR module will then fire its completion event.
                Debug.Log("MANAGER: Silence detected, stopping ASR.");
                TransitionToState(AssistantState.Processing);
            }
        }

        private void HandleAsrRecognitionStreaming(string result)
        {
            // 检查是否是错误信息（简单的错误检测）
            if (result.Contains("连接超时") || result.Contains("网络连接失败") || 
                result.Contains("设备错误") || result.Contains("处理失败"))
            {
                Debug.LogError($"ASR错误: {result}");
                _conversationData.ErrorInfo = $"语音识别失败: {result}";
                UpdateStatusText();
                TransitionToState(AssistantState.Invalid);
                return;
            }
            
            // 正常处理ASR结果
            if (_currentState != AssistantState.Listening) return;
            _conversationData.AsrResult = result;
            UpdateStatusText();
        }

        private void HandleLlmProcessingComplete(LlmResult result)
        {
            if (_currentState != AssistantState.Processing) return;
            
            // 检查是否是错误结果
            if (result.IsError)
            {
                Debug.LogError($"LLM错误: {result.ErrorMessage}");
                _conversationData.ErrorInfo = $"智能处理失败: {result.ErrorMessage}";
                UpdateStatusText();
                TransitionToState(AssistantState.Invalid);
                return;
            }
            
            // 如果没有工具调用，视为无效请求
            if (!result.HasToolCall)
            {
                Debug.Log($"LLM没有工具调用: {result.Response}");
                _conversationData.ErrorInfo = result.Response;
                UpdateStatusText();
                TransitionToState(AssistantState.Invalid);
                return;
            }
            
            // 正常处理LLM结果（有工具调用）
            _conversationData.LlmResult = result.Response;
            UpdateStatusText();
            TransitionToState(AssistantState.Success);
        }

        private void HandleAsrError(string errorMessage)
        {
            Debug.LogError($"ASR Error: {errorMessage}");
            _conversationData.ErrorInfo = $"语音识别失败: {errorMessage}";
            UpdateStatusText();
            TransitionToState(AssistantState.Invalid);
        }

        private void HandleLlmError(string errorMessage)
        {
            Debug.LogError($"LLM Error: {errorMessage}");
            _conversationData.ErrorInfo = $"智能处理失败: {errorMessage}";
            UpdateStatusText();
            TransitionToState(AssistantState.Invalid);
        }

        #endregion

        private void UpdateStatusText()
        {
            var displayText = new StringBuilder();
            Color wakeWordRGBA = statusTextColor;
            wakeWordRGBA.a = _conversationData.WakeWordConfidence;

            displayText.Append($"<color=#{ColorUtility.ToHtmlStringRGBA(statusTextColor)}>");
            displayText.Append($"{_conversationData.WakeWord}</color>, ");
            displayText.Append(_conversationData.AsrResult);
            displayText.AppendLine(_conversationData.LlmResult);
            // Error info
            displayText.Append($"<color=#FFFFFF><size=12>{_conversationData.ErrorInfo}</size></color>");
            statusText.text = displayText.ToString();
        }
    }
}