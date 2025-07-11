
namespace VAI
{
    using System;
    using UnityEngine;
    using UnityEngine.UI;
    using System.Text;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Newtonsoft.Json;
    using System.IO;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using VAI;

    #region Data Structures for API Communication

    public class ChatCompletionResponse
    {
        public List<Choice> choices { get; set; }
    }

    public class Choice
    {
        public Message message { get; set; }
    }

    public class Message
    {
        public string role { get; set; }
        public string content { get; set; }
        
        // Legacy function call support (deprecated)
        public FunctionCall function_call { get; set; }
        
        // New tool calls support (current)
        public List<ToolCall> tool_calls { get; set; }
    }

    // Legacy function call class (deprecated but kept for compatibility)
    public class FunctionCall
    {
        public string name { get; set; }
        public string arguments { get; set; }
    }

    // New tool call classes
    public class ToolCall
    {
        public string id { get; set; }
        public string type { get; set; }
        public FunctionCall function { get; set; }
    }

    // Tool definition for request
    public class Tool
    {
        public string type { get; set; } = "function";
        public FunctionDefinition function { get; set; }
    }

    public class FunctionParameter
    {
        public string type { get; set; }
        public string description { get; set; }
        [JsonProperty("enum", NullValueHandling = NullValueHandling.Ignore)]
        public List<string> @enum { get; set; }
    }

    public class FunctionDefinition
    {
        public string name { get; set; }
        public string description { get; set; }
        public ParametersDefinition parameters { get; set; }
    }
    
    public class ParametersDefinition
    {
        public string type { get; set; } = "object";
        public Dictionary<string, FunctionParameter> properties { get; set; }
        public List<string> required { get; set; }
    }
    
    #endregion

    #region Function Argument Classes
    // 移除所有参数类，改为自动生成
    #endregion

    public class apiFuncCalling : MonoBehaviour
    {
        [Header("API Configuration")]
        public string apiKey;
        public string modelName = "qwen-max";
        public string systemRole;
        
        [Header("Dependencies")]
        public FuncCallingList funcCallingList;
        
        [Header("Tool Calling Settings")]
        [Tooltip("Enable parallel tool calling - allows multiple tools to be called simultaneously")]
        public bool enableParallelToolCalls = true;
        
        [Tooltip("Enable backward compatibility with old function call format")]
        public bool enableLegacyFunctionCalls = true;

        [Header("Timeout Settings")]
        [Tooltip("Maximum time to wait for LLM response (seconds)")]
        public float requestTimeoutSeconds = 30f;

        [Header("UI for Testing")]
        public Text userInputField;
        public Text testResponseText;

        // Core properties
        public bool IsProcessing { get; private set; }
        public string LastError { get; private set; } = "";
        
        // HTTP client (reused for performance)
        private static readonly HttpClient _httpClient = new HttpClient();
        private CancellationTokenSource _currentRequestCts;
        
        // 缓存工具定义以提高性能
        private List<Tool> _cachedToolDefinitions;

        #region Unity Lifecycle

        void Start()
        {
            InitializeHttpClient();
            
            if (funcCallingList == null)
            {
                Debug.LogError("FuncCallingList未设置！");
            }
            
            Debug.Log("API函数调用控制器初始化完成");
        }

        void OnDestroy()
        {
            _currentRequestCts?.Cancel();
            _currentRequestCts?.Dispose();
        }

        private void InitializeHttpClient()
        {
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
            _httpClient.DefaultRequestHeaders.Clear();
            
            // Set a reasonable timeout
            _httpClient.Timeout = TimeSpan.FromSeconds(requestTimeoutSeconds + 5);
        }

        #endregion

        #region Public API

        /// <summary>
        /// 处理命令的主要入口点
        /// </summary>
        /// <param name="command">用户命令</param>
        /// <returns>是否有工具被调用</returns>
        public async Task<bool> ProcessCommand(string command)
        {
            if (IsProcessing)
            {
                Debug.LogWarning("正在处理另一个命令，请稍候");
                return false;
            }

            if (string.IsNullOrWhiteSpace(command))
            {
                UpdateUI("错误：收到空命令", "命令不能为空");
                return false;
            }

            try
            {
                IsProcessing = true;
                _currentRequestCts = new CancellationTokenSource();
                
                UpdateUI("处理中...", "正在分析命令...");
                
                Debug.Log($"开始处理命令: {command}");
                
                var result = await ProcessCommandInternal(command, _currentRequestCts.Token);
                
                UpdateUI("完成", result.response);
                Debug.Log($"命令处理完成，工具调用: {result.hasToolCall}");
                
                return result.hasToolCall;
            }
            catch (OperationCanceledException)
            {
                Debug.Log("命令处理被取消");
                UpdateUI("已取消", "命令处理被取消");
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"处理命令时出错: {ex.Message}");
                string errorMessage = FormatUserFriendlyError(ex);
                UpdateUI("错误", errorMessage);
                LastError = errorMessage;
                return false;
            }
            finally
            {
                IsProcessing = false;
                _currentRequestCts?.Dispose();
                _currentRequestCts = null;
            }
        }

        /// <summary>
        /// 取消当前正在进行的请求
        /// </summary>
        public void CancelCurrentRequest()
        {
            if (IsProcessing)
            {
                Debug.Log("取消当前请求");
                _currentRequestCts?.Cancel();
            }
        }

        #endregion

        #region Core Processing Logic

        private async Task<(string response, bool hasToolCall)> ProcessCommandInternal(string command, CancellationToken cancellationToken)
        {
            // 1. 调用LLM获取响应
            var llmResponse = await CallLLMApi(command, cancellationToken);
            
            // 2. 解析响应
            var parsedResponse = ParseLLMResponse(llmResponse);
            
            // 3. 执行工具调用（如果有）
            if (parsedResponse.hasToolCalls)
            {
                var toolResults = await ExecuteToolCalls(parsedResponse.toolCalls, cancellationToken);
                return (toolResults, true);
            }
            else
            {
                return (parsedResponse.content ?? "模型返回了空响应", false);
            }
        }

        private async Task<string> CallLLMApi(string prompt, CancellationToken cancellationToken)
        {
            string endpoint = "https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions";
            
            var requestData = new
            {
                model = modelName,
                messages = new[]
                {
                    new { role = "system", content = systemRole },
                    new { role = "user", content = prompt }
                },
                tools = GetToolDefinitions(),
                tool_choice = "auto",
                parallel_tool_calls = enableParallelToolCalls
            };

            string jsonData = JsonConvert.SerializeObject(requestData);
            
            // 性能优化：只在Debug模式下记录请求详情
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"发送API请求: {jsonData.Substring(0, Math.Min(200, jsonData.Length))}...");
            #endif

            using var request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(endpoint),
                Content = new StringContent(jsonData, Encoding.UTF8, "application/json")
            };
            
            request.Headers.Add("Authorization", $"Bearer {apiKey}");

            // 使用带超时的请求
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(requestTimeoutSeconds));

            var response = await _httpClient.SendAsync(request, timeoutCts.Token);

            if (!response.IsSuccessStatusCode)
            {
                string errorContent = await response.Content.ReadAsStringAsync();
                throw new HttpRequestException($"API请求失败 ({response.StatusCode}): {errorContent}");
            }

            string result = await response.Content.ReadAsStringAsync();
            
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"API响应: {result.Substring(0, Math.Min(300, result.Length))}...");
            #endif
            
            return result;
        }

        private (string content, bool hasToolCalls, List<ToolCall> toolCalls) ParseLLMResponse(string apiResponse)
        {
            try
            {
                var parsedResult = JsonConvert.DeserializeObject<ChatCompletionResponse>(apiResponse);
                var message = parsedResult.choices[0].message;

                // 检查新格式的工具调用
                if (message.tool_calls != null && message.tool_calls.Count > 0)
                {
                    Debug.Log($"检测到 {message.tool_calls.Count} 个工具调用");
                    return (null, true, message.tool_calls);
                }
                // 兼容旧格式的函数调用
                else if (enableLegacyFunctionCalls && message.function_call != null)
                {
                    Debug.Log("检测到旧格式函数调用");
                    var legacyToolCall = new ToolCall
                    {
                        id = Guid.NewGuid().ToString(),
                        type = "function",
                        function = message.function_call
                    };
                    return (null, true, new List<ToolCall> { legacyToolCall });
                }
                else
                {
                    return (message.content, false, null);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"解析LLM响应失败: {ex.Message}");
            }
        }

        private async Task<string> ExecuteToolCalls(List<ToolCall> toolCalls, CancellationToken cancellationToken)
        {
            var results = new List<string>();
            var errors = new List<string>();
            
            if (enableParallelToolCalls && toolCalls.Count > 1)
            {
                // 并行执行
                var tasks = toolCalls.Select(async toolCall =>
                {
                    try
                    {
                        return await ExecuteSingleToolCall(toolCall, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        return $"工具调用 {toolCall.id} 失败: {ex.Message}";
                    }
                });

                var taskResults = await Task.WhenAll(tasks);
                results.AddRange(taskResults);
            }
            else
            {
                // 顺序执行
                foreach (var toolCall in toolCalls)
                {
                    try
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        
                        var result = await ExecuteSingleToolCall(toolCall, cancellationToken);
                        results.Add(result);
                        
                        // 给每个操作一些时间在Unity主线程中完成
                        await Task.Delay(100, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"工具调用 {toolCall.id} 失败: {ex.Message}");
                    }
                }
            }
            
            return FormatExecutionResults(results, errors);
        }

        private async Task<string> ExecuteSingleToolCall(ToolCall toolCall, CancellationToken cancellationToken)
        {
            if (toolCall.type != "function" || toolCall.function == null)
            {
                throw new ArgumentException($"不支持的工具类型: {toolCall.type}");
            }

            Debug.Log($"执行工具调用: {toolCall.function.name}");

            // 在主线程中执行函数调用
            string result = "";
            await UnityMainThreadDispatcher.Instance().EnqueueAsync(() =>
            {
                result = ExecuteLocalFunction(toolCall.function.name, toolCall.function.arguments);
            });
            return result;
        }

        #endregion

        #region Tool Definitions and Execution

        private List<Tool> GetToolDefinitions()
        {
            // 自动从FunctionRegistry生成Tool定义
            return VAI.FunctionRegistry.All().Select(meta => new Tool
            {
                type = "function",
                function = new FunctionDefinition
                {
                    name = meta.Name,
                    description = meta.Description,
                    parameters = new ParametersDefinition
                    {
                        properties = meta.Parameters.ToDictionary(
                            kv => kv.Key,
                            kv => new FunctionParameter
                            {
                                type = kv.Value.Type,
                                description = kv.Value.Description,
                                @enum = kv.Value.Enum
                            }
                        ),
                        required = meta.Parameters.Keys.ToList()
                    }
                }
            }).ToList();
        }

        private string ExecuteLocalFunction(string functionName, string argumentsJson)
        {
            var meta = VAI.FunctionRegistry.Get(functionName);
            var args = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, object>>(argumentsJson);
            return meta.Execute(args);
        }

        #endregion

        #region Helper Methods

        private string FormatExecutionResults(List<string> results, List<string> errors)
        {
            var response = new StringBuilder();
            
            if (results.Count > 0)
            {
                response.AppendLine($"成功执行了 {results.Count} 个操作:");
                foreach (var result in results)
                {
                    response.AppendLine($"✓ {result}");
                }
            }
            
            if (errors.Count > 0)
            {
                if (results.Count > 0) response.AppendLine();
                response.AppendLine($"发生 {errors.Count} 个错误:");
                foreach (var error in errors)
                {
                    response.AppendLine($"✗ {error}");
                }
            }
            
            return response.ToString().Trim();
        }

        private void UpdateUI(string status, string response)
        {
            if (testResponseText != null) testResponseText.text = response;
            
            // 也更新语音助手的UI
            var assistantManager = GetComponent<AliyunVoiceAssistManager>();
            if (assistantManager?.VaiResponse != null)
            {
                assistantManager.VaiResponse.text = response;
            }
        }

        private string FormatUserFriendlyError(Exception ex)
        {
            return ex switch
            {
                TimeoutException => "请求超时，请重试",
                HttpRequestException => "网络连接失败，请检查网络设置",
                ArgumentException => "参数错误，请重新表述您的需求",
                InvalidOperationException => "操作失败，请重试",
                _ => "处理失败，请重试"
            };
                }

        #endregion

        #region Test Methods (for debugging)

        public async void SendMessageFromInputField()
        {
            if (userInputField == null) return;
            
            string userInput = userInputField.text;
            bool hasToolCall = await ProcessCommand(userInput);
            Debug.Log($"测试结果 - 工具调用: {hasToolCall}");
        }

        #endregion
    }
}