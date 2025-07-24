using UnityEngine;
using VAI;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.UI;

// 这个组件负责为当前场景向LlmController注册特定的函数
public class FuncRegistryExample : MonoBehaviour
{
    //public LlmController llmController;
    public FuncCallExample functionImplementations;

    [Header("Optional")]
    [Tooltip("You can either register functions in the inspector or in the code")]
    public List<SerializableFunctionMeta> userRegisteredFunctions;
    [Tooltip("Optional, display all functions in the inspector")]
    public Text functionListText;

    private void RegisterSceneFunctions()
    {
        var registry = LlmController.Instance.functionRegistry;

        // --- 在下面定义和注册所有此场景需要的函数 ---
        
        registry.Register(new FunctionMeta
        {
            Name = "ModifyTransform",
            Description = "修改物体的变换属性，包括移动、旋转和缩放。",
            Parameters = new Dictionary<string, ParameterMeta>
            {
                { "objectName", new ParameterMeta { Type = ParameterMeta.ParamType.String, Description = "物体的名字", Enum = new List<string> { "cube", "sphere", "capsule", "main camera" } } },
                { "transformType", new ParameterMeta { Type = ParameterMeta.ParamType.String, Description = "transform的维度", Enum = new List<string> { "moveleft", "moveright", "movebackward", "moveforward", "moveup", "movedown", "pitch", "yaw", "roll", "scale" } } },
                { "number", new ParameterMeta { Type = ParameterMeta.ParamType.Number, Description = "给物体transform某维度改变的数值，不能出现负数。" } }
            },
            Execute = args =>
            {
                var objectName = args["objectName"].ToString();
                var transformType = args["transformType"].ToString();
                var number = Convert.ToSingle(args["number"]);
                return functionImplementations.ModifyTransform(objectName, transformType, number);
            }
        });
        // --- 继续注册新的函数 ---

        //// 该函数已在inspector中注册. This function has been registered in the inspector
        // registry.Register(new FunctionMeta
        // {
        //     Name = "ChangeObjectColor",
        //     Description = "改变物体的颜色。",
        //     Parameters = new Dictionary<string, ParameterMeta>
        //     {
        //         { "objectName", new ParameterMeta { Type = ParameterMeta.ParamType.String, Description = "物体的名字", Enum = new List<string> { "cube", "sphere", "capsule" } } },
        //         { "hexColor", new ParameterMeta { Type = ParameterMeta.ParamType.String, Description = "16进制颜色代码" } }
        //     },
        //     Execute = args =>
        //     {
        //         var objectName = args["objectName"].ToString();
        //         var hexColor = args["hexColor"].ToString();
        //         // 调用函数
        //         return functionImplementations.ChangeObjectColor(objectName, hexColor);
        //     }
        // });

        // --- 注册在 Inspector 中定义的函数 ---
        RegisterFunctionsFromInspector(registry);
    }
    void Start()
    {
        if (LlmController.Instance == null)
        {
            Debug.LogError("LlmController.Instance is not available. Ensure an LlmController is active in your scene.", this);
            return;
        }
        if (functionImplementations == null)
        {
            Debug.LogError("Function Implementations component is not assigned!", this);
            return;
        }

        // 清理之前场景或脚本注册的所有函数
        LlmController.Instance.ClearFunctionRegistry();
        RegisterSceneFunctions();

        //更新UI
        if (functionListText)
        {
            functionListText.text = LlmController.Instance.functionRegistry.GetAllFunctionsAsFormattedString();
        }
    }

    void OnDisable()
    {
        if (LlmController.Instance != null)
        {
            // 清理本脚本注册的函数
            LlmController.Instance.ClearFunctionRegistry();
            Debug.Log($"Functions from {gameObject.name} have been unregistered.", this);
            // if (functionListText)
            // {
            //     functionListText.text = "";
            // }
        }
    }

    private void RegisterFunctionsFromInspector(FunctionRegistry registry)
    {
        foreach (var sFunc in userRegisteredFunctions)
        {
            if (string.IsNullOrEmpty(sFunc.Name))
            {
                Debug.LogWarning("A user registered function has no name and will be skipped.");
                continue;
            }

            // 1. 将参数列表转换为字典
            var parameters = new Dictionary<string, ParameterMeta>();
            foreach (var sParam in sFunc.Parameters)
            {
                if (string.IsNullOrEmpty(sParam.Name))
                {
                     Debug.LogWarning($"A parameter in function '{sFunc.Name}' has no name and will be skipped.");
                     continue;
                }
                parameters[sParam.Name] = new ParameterMeta
                {
                    Type = (ParameterMeta.ParamType)sParam.Type,
                    Description = sParam.Description,
                    Enum = sParam.Enum
                };
            }

            // 2. 使用反射动态创建 Execute 委托
            var executeAction = new Func<IDictionary<string, object>, string>(args =>
            {
                MethodInfo methodInfo = functionImplementations.GetType().GetMethod(sFunc.Name);
                if (methodInfo == null)
                {
                    string errorMsg = $"Error: Method '{sFunc.Name}' not found in {functionImplementations.GetType().Name}.";
                    Debug.LogError(errorMsg);
                    return errorMsg;
                }

                ParameterInfo[] methodParams = methodInfo.GetParameters();
                object[] callArgs = new object[methodParams.Length];

                for (int i = 0; i < methodParams.Length; i++)
                {
                    ParameterInfo pInfo = methodParams[i];
                    if (args.TryGetValue(pInfo.Name, out object argValue))
                    {
                        try
                        {
                            // 自动进行类型转换，例如JSON中的数字通常是double，需要转为float或int
                            callArgs[i] = Convert.ChangeType(argValue, pInfo.ParameterType);
                        }
                        catch (Exception ex)
                        {
                            string errorMsg = $"Error converting parameter '{pInfo.Name}' for method '{sFunc.Name}': {ex.Message}";
                            Debug.LogError(errorMsg);
                            return errorMsg;
                        }
                    }
                    else
                    {
                        string errorMsg = $"Error: Missing argument '{pInfo.Name}' for method '{sFunc.Name}'.";
                        Debug.LogError(errorMsg);
                        return errorMsg;
                    }
                }

                try
                {
                    // 调用实际的方法
                    object result = methodInfo.Invoke(functionImplementations, callArgs);
                    return result?.ToString() ?? string.Empty;
                }
                catch (Exception ex)
                {
                    // 捕获并报告方法执行期间的异常
                    string errorMsg = $"Error during execution of '{sFunc.Name}': {ex.InnerException?.Message ?? ex.Message}";
                    Debug.LogError(errorMsg);
                    return errorMsg;
                }
            });


            // 3. 创建并注册完整的 FunctionMeta
            var meta = new FunctionMeta
            {
                Name = sFunc.Name,
                Description = sFunc.Description,
                Parameters = parameters,
                Execute = executeAction
            };

            registry.Register(meta);
        }
    }
}