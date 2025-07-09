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

    #region 服务器通讯用的数据结构
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
        public FunctionCall function_call { get; set; }
    }

    public class FunctionCall // 新增函数调用类
    {
        public string name { get; set; }
        public string arguments { get; set; }
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

    #region 函数解析用的数据结构
    
    public class TransformObjectArguments
    {
        public string objectName { get; set; }
        public string transformType { get; set; }
        public float number { get; set; }
    }
    
    public class ChangeObjectColorArguments
    {
        public string objectName { get; set; }
        public string hexColor { get; set; }
    }
        
    #endregion

    public class apiFuncCalling : MonoBehaviour
    {
        private bool buttonSendMessage = false;
        public string apiKey;
        public string modelName = "qwen-max";

        public string systemRole;
        public FuncCallingList funcCallingList;
        private string userInput;
        private string response;

        [Header("TEST only")] public Text userInputField;
        public Text testResponseText;

        private static readonly HttpClient httpClient = new HttpClient();

        void Start()
        {
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
            //httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public async Task ProcessCommand(string command)
        {
            userInput = userInputField.text;
            if (string.IsNullOrEmpty(command))
            {
                testResponseText.text = "Error: Received an empty command.";
                return;
            }

            testResponseText.text = "Processing...";
            AliyunVoiceAssistManager assistantManager = GetComponent<AliyunVoiceAssistManager>();
            assistantManager.VaiResponse.text = "Processing...";
            
            print($"apiFuncCalling processing command: {command}");
            response = await GetLLMResponse(command);
            testResponseText.text = response;
            assistantManager.VaiResponse.text = response;
            print("LLM Response: " + response);
            assistantManager.OnFunctionCalled();
        }

        // 获取函数定义列表
        private List<FunctionDefinition> GetFunctionDefinitions()
        {
            return new List<FunctionDefinition>
            {
                new FunctionDefinition
                {
                    name = "ModifyTransform",
                    description = "",
                    parameters = new ParametersDefinition
                    {
                        properties = new Dictionary<string, FunctionParameter>
                        {
                            { "objectName", new FunctionParameter
                            {
                                type = "string", 
                                description = "物体的名字",
                                @enum = new List<string> { "cube", "sphere", "capsule", "main camera" }
                            }},
                            { "transformType", new FunctionParameter { 
                                type = "string", 
                                description = "transform的维度", 
                                @enum = new List<string> { "moveleft", "moveright", "movebackward", "moveforward", "moveup", "movedown", "pitch", "yaw", "roll", "scale" }
                            }},
                            { "number", new FunctionParameter
                            {
                                type = "number", 
                                description = "给物体transform某维度改变的数值，不能出现负数，物体只有30cm大，摄像机只能移动和旋转（镜像）针对摄像机转动的幅度要小"
                            } }
                        },
                        required = new List<string> { "objectName", "transformType", "number" }
                    }
                },
                new FunctionDefinition
                {
                    name = "ChangeObjectColor",
                    description = "改变物体的颜色",
                    parameters = new ParametersDefinition
                    {
                        properties = new Dictionary<string, FunctionParameter>
                        {
                            { "objectName", new FunctionParameter
                            {
                                type = "string", 
                                description = "物体的名字",
                                @enum = new List<string> { "cube", "sphere", "capsule", "main camera" }
                            }},
                            { "hexColor", new FunctionParameter { 
                                type = "string", 
                                description = "hex color code"
                            }}
                        },
                        required = new List<string> { "objectName", "hexColor"}
                    }
                }
            };
        }

        // 执行本地函数
        private string ExecuteLocalFunction(string functionName, string argumentsJson)
        {
            try
            {
                switch (functionName)
                {
                    case "ModifyTransform":
                        TransformObjectArguments argsTransformObject = JsonConvert.DeserializeObject<TransformObjectArguments>(argumentsJson);
                        funcCallingList.ModifyTransform(argsTransformObject.objectName, argsTransformObject.transformType, argsTransformObject.number); 
                        return $"成功: Game object= '{argsTransformObject.objectName}', transformType = {argsTransformObject.transformType}, number = {argsTransformObject.number}.";

                    case "ChangeObjectColor":
                        ChangeObjectColorArguments argsChangeObjColor = JsonConvert.DeserializeObject<ChangeObjectColorArguments>(argumentsJson);
                        funcCallingList.ChangeObjectColor(argsChangeObjColor.objectName, argsChangeObjColor.hexColor);
                        return "颜色已修改";
                    
                    default:
                        return $"未知函数: {functionName}";
                }
            }
            catch (Exception ex)
            {
                return $"函数执行错误: {ex.Message}";
            }
        }


        private async Task<string> GetLLMResponse(string prompt)
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
                functions = GetFunctionDefinitions(),
                function_call = "auto"
            };

            string jsonData = JsonConvert.SerializeObject(requestData);
            print("\n request jsondata" + jsonData);

            HttpRequestMessage request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new System.Uri(endpoint),
                Headers =
                {
                    { "Authorization", $"Bearer {apiKey}" }
                },
                Content = new StringContent(jsonData, System.Text.Encoding.UTF8, "application/json")
            };
            // 直接设置 Content-Type
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            print("\n request \n" + request);

            HttpResponseMessage response = await httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                string result = await response.Content.ReadAsStringAsync();
                ChatCompletionResponse parsedResult = JsonConvert.DeserializeObject<ChatCompletionResponse>(result);
                var message = parsedResult.choices[0].message;

                // 检查是否有函数调用
                if (message.function_call != null)
                {
                    print(message.content);
                    // 执行本地函数
                    return ExecuteLocalFunction(message.function_call.name,  message.function_call.arguments);
                }
                else
                {
                    return message.content;
                }
            }
            else
            {
                return $"请求失败：{response.StatusCode}";
            }
        }

        // ---- for test only ----
        public void SendMessageFromInputField()
        {
            userInput = userInputField.text;
            _ = ProcessCommand(userInput); // Call the main method
        }

        private void Update()
        {
            if (buttonSendMessage)
            {
                SendMessageFromInputField();
                buttonSendMessage = false;
            }
        }
        // ------------------------
    }
}