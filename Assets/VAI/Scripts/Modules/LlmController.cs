using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using System.Net.Http;
using System.Linq;
using Newtonsoft.Json;
using System.IO;

namespace VAI
{
    // LLM处理结果数据结构
    public class LlmResult
    {
        public string Response { get; set; }
        public bool IsError { get; set; }
        public string ErrorMessage { get; set; }
        public bool HasToolCall { get; set; }
        
        public static LlmResult Success(string response, bool hasToolCall = false) => 
            new LlmResult { Response = response, IsError = false, HasToolCall = hasToolCall };
        
        public static LlmResult Error(string errorMessage) => 
            new LlmResult { Response = "", IsError = true, ErrorMessage = errorMessage, HasToolCall = false };
    }

    // API通信数据结构
    [System.Serializable]
    public class ChatCompletionResponse
    {
        public List<Choice> choices { get; set; }
    }

    [System.Serializable]
    public class Choice
    {
        public Message message { get; set; }
    }

    [System.Serializable]
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
    [System.Serializable]
    public class FunctionCall
    {
        public string name { get; set; }
        public string arguments { get; set; }
    }

    // New tool call classes
    [System.Serializable]
    public class ToolCall
    {
        public string id { get; set; }
        public string type { get; set; }
        public FunctionCall function { get; set; }
    }

    // Tool definition for request
    [System.Serializable]
    public class Tool
    {
        public string type { get; set; } = "function";
        public FunctionDefinition function { get; set; }
    }

    [System.Serializable]
    public class FunctionParameter
    {
        public string type { get; set; }
        public string description { get; set; }
        [JsonProperty("enum", NullValueHandling = NullValueHandling.Ignore)]
        public List<string> @enum { get; set; }
    }

    [System.Serializable]
    public class FunctionDefinition
    {
        public string name { get; set; }
        public string description { get; set; }
        public ParametersDefinition parameters { get; set; }
    }

    [System.Serializable]
    public class ParametersDefinition
    {
        public string type { get; set; } = "object";
        public Dictionary<string, FunctionParameter> properties { get; set; }
        public List<string> required { get; set; }
    }

    public class LlmController : MonoBehaviour
    {
        [Header("Api")]
        [Tooltip("Aliyun DashScope API Key, leave empty to use environment variable DASHSCOPE_API_KEY")]
        public string apiKey = "DASHSCOPE_API_KEY";
        [Tooltip("Model name")]
        public string modelName = "qwen-turbo";
        [Tooltip("System role")]
        [TextArea(6, 12)]
        public string systemRole = "你是一个智能助手，帮助用户控制Unity场景中的物体。";
        
        [Header("Timeout")]
        [Tooltip("Request timeout (seconds)")]
        public float requestTimeoutSeconds = 30f;
        
        [Header("Tool")]
        [Tooltip("启用并行工具调用")]
        public bool enableParallelToolCalls = true;
        [Tooltip("启用向后兼容的旧函数调用格式")]
        public bool enableLegacyFunctionCalls = true;

        // Manager接口事件
        public event Action<LlmResult> OnProcessingComplete;
        public event Action<string> Error;

        // 依赖
        public FuncCallingList funcCallingList;

        // 私有状态
        private bool _isProcessing = false;
        private static readonly HttpClient _httpClient = new HttpClient();
        private CancellationTokenSource _currentRequestCts;
        private List<Tool> _cachedToolDefinitions;
        
        // 实际使用的API Key
        private string _effectiveApiKey;

        #region Unity lifecycle

        void Start()
        {
            InitializeHttpClient();

            if (string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable(apiKey)))
            {
                throw new InvalidOperationException("API Key not found, plz add DASHSCOPE_API_KEY to environment variables");
            }
            else
            {
                _effectiveApiKey = System.Environment.GetEnvironmentVariable(apiKey);
            }
            
            if (funcCallingList == null)
            {
                funcCallingList = FindObjectOfType<FuncCallingList>();
                if (funcCallingList == null)
                {
                    Debug.LogError("FuncCallingList未找到！");
                }
            }
            
            Debug.Log("LLM控制器初始化完成");
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
            _httpClient.Timeout = TimeSpan.FromSeconds(requestTimeoutSeconds + 5);
        }

        #endregion

        #region Manager接口实现

        public void ProcessCommand(string command)
        {
            if (_isProcessing)
            {
                Debug.LogWarning("LLM正在处理另一个命令，请稍候");
                return;
            }

            if (string.IsNullOrWhiteSpace(command))
            {
                Error?.Invoke("命令不能为空");
                return;
            }

            Debug.Log($"LLM: 开始处理命令: {command}");
            _ = ProcessCommandAsync(command);
        }

        public void CancelProcessing()
        {
            Debug.Log("LLM: 取消处理");
            _currentRequestCts?.Cancel();
            _isProcessing = false;
        }

        #endregion

        #region 核心LLM处理逻辑

        private async Task ProcessCommandAsync(string command)
        {
            try
            {
                _isProcessing = true;
                _currentRequestCts = new CancellationTokenSource();
                
                var result = await ProcessCommandInternal(command, _currentRequestCts.Token);
                
                UnityMainThreadDispatcher.Instance().Enqueue(() =>
                {
                    OnProcessingComplete?.Invoke(result);
                });
            }
            catch (OperationCanceledException)
            {
                Debug.Log("LLM处理被取消");
            }
            catch (Exception ex)
            {
                Debug.LogError($"LLM处理失败: {ex.Message}");
                string errorMessage = FormatUserFriendlyError(ex);
                UnityMainThreadDispatcher.Instance().Enqueue(() => Error?.Invoke(errorMessage));
            }
            finally
            {
                _isProcessing = false;
                _currentRequestCts?.Dispose();
                _currentRequestCts = null;
            }
        }

        private async Task<LlmResult> ProcessCommandInternal(string command, CancellationToken cancellationToken)
        {
            // 1. 调用LLM API
            var llmResponse = await CallLLMApi(command, cancellationToken);
            
            // 2. 解析响应
            var parsedResponse = ParseLLMResponse(llmResponse);
            
            // 3. 执行工具调用（如果有）
            if (parsedResponse.hasToolCalls)
            {
                var toolResults = await ExecuteToolCalls(parsedResponse.toolCalls, cancellationToken);
                return LlmResult.Success(toolResults, hasToolCall: true);
            }
            else
            {
                var content = parsedResponse.content ?? "模型返回了空响应";
                return LlmResult.Success(content, hasToolCall: false);
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
            
            #if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"发送LLM请求: {jsonData}");
            #endif

            using var request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(endpoint),
                Content = new StringContent(jsonData, Encoding.UTF8, "application/json")
            };
            
            request.Headers.Add("Authorization", $"Bearer {_effectiveApiKey}");

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
            Debug.Log($"LLM API响应: {result.Substring(0, Math.Min(300, result.Length))}...");
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

        #region 工具定义和执行

        private List<Tool> GetToolDefinitions()
        {
            if (_cachedToolDefinitions == null)
            {
                _cachedToolDefinitions = FunctionRegistry.All().Select(meta => new Tool
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
            return _cachedToolDefinitions;
        }

        private string ExecuteLocalFunction(string functionName, string argumentsJson)
        {
            try
            {
                var meta = FunctionRegistry.Get(functionName);
                var args = JsonConvert.DeserializeObject<Dictionary<string, object>>(argumentsJson);
                return meta.Execute(args);
            }
            catch (Exception ex)
            {
                Debug.LogError($"执行函数 {functionName} 失败: {ex.Message}");
                return $"错误: 执行函数失败 - {ex.Message}";
            }
        }

        #endregion

        #region 辅助方法

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
    }
}