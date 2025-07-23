using UnityEngine;
using VAI;
using System;
using System.Collections.Generic;

// 这个组件负责为当前场景向LlmController注册特定的函数
public class FuncRegistryExample : MonoBehaviour
{
    [Header("依赖组件")]
    [Tooltip("持有函数注册表(Function Registry)的LlmController")]
    public LlmController llmController;

    [Tooltip("包含了具体函数实现逻辑的组件")]
    public CharFuncCallingList functionImplementations;

    void Awake()
    {
        if (llmController == null)
        {
            Debug.LogError("CharFunctionRegistry 未指定 LlmController！", this);
            return;
        }

        if (functionImplementations == null)
        {
            Debug.LogError("CharFunctionRegistry 未指定 FunctionImplementations (CharFuncCallingList)！", this);
            return;
        }

        RegisterSceneFunctions();
    }

    private void RegisterSceneFunctions()
    {
        var registry = llmController.functionRegistry;

        // --- 在下面定义和注册所有此场景需要的函数 ---
        
        registry.Register(new FunctionMeta
        {
            Name = "ModifyTransform",
            Description = "修改物体的变换属性，包括移动、旋转和缩放。",
            Parameters = new Dictionary<string, ParameterMeta>
            {
                { "objectName", new ParameterMeta { Type = "string", Description = "物体的名字", Enum = new List<string> { "cube", "sphere", "capsule", "main camera" } } },
                { "transformType", new ParameterMeta { Type = "string", Description = "transform的维度", Enum = new List<string> { "moveleft", "moveright", "movebackward", "moveforward", "moveup", "movedown", "pitch", "yaw", "roll", "scale" } } },
                { "number", new ParameterMeta { Type = "number", Description = "给物体transform某维度改变的数值，不能出现负数。" } }
            },
            Execute = args =>
            {
                var objectName = args["objectName"].ToString();
                var transformType = args["transformType"].ToString();
                var number = Convert.ToSingle(args["number"]);
                // 调用在 Inspector 窗口中指定的组件的函数
                return functionImplementations.ModifyTransform(objectName, transformType, number);
            }
        });

        registry.Register(new FunctionMeta
        {
            Name = "ChangeObjectColor",
            Description = "改变物体的颜色。",
            Parameters = new Dictionary<string, ParameterMeta>
            {
                { "objectName", new ParameterMeta { Type = "string", Description = "物体的名字", Enum = new List<string> { "cube", "sphere", "capsule" } } },
                { "hexColor", new ParameterMeta { Type = "string", Description = "16进制颜色代码" } }
            },
            Execute = args =>
            {
                var objectName = args["objectName"].ToString();
                var hexColor = args["hexColor"].ToString();
                // 调用在 Inspector 窗口中指定的组件的函数
                return functionImplementations.ChangeObjectColor(objectName, hexColor);
            }
        });

        registry.Register(new FunctionMeta
        {
            Name = "SetTimeOfDay",
            Description = "设置0-24小时的时间",
            Parameters = new Dictionary<string, ParameterMeta> {
                { "timeOfDay", new ParameterMeta { Type = "number", Description = "0-24" } }
            },
            Execute = args =>
            {
                return functionImplementations.SetTimeOfDay(Convert.ToSingle(args["timeOfDay"]));
            }
        });

        registry.Register(new FunctionMeta
        {
            Name = "ReplaceCustomAnimMotion",
            Description = "给游戏角色添加一个支持的动画",
            Parameters = new Dictionary<string, ParameterMeta> {
                { "clipName", new ParameterMeta { Type = "string", Description = "动画的名称,请严格匹配", Enum = new List<string> { "aerial_cartwheel", "greeting_or_goodbye_wave", "sing_gesture","backflip_somersault","check_backside_then_shrug" } } }
            },
            Execute = args =>
            {
                return functionImplementations.ReplaceCustomAnimMotion(args["clipName"].ToString());
            }
        });

        registry.Register(new FunctionMeta
        {
            Name = "SetExpressionHappy",
            Description = "设置游戏角色的表情为开心",
            Parameters = new Dictionary<string, ParameterMeta> { },
            Execute = args =>
            {
                return functionImplementations.SetExpressionHappy();
            }
        });

        registry.Register(new FunctionMeta
            {
                Name = "SetExpressionSad",
                Description = "设置游戏角色的表情为悲伤",
                Parameters = new Dictionary<string, ParameterMeta> { },
                Execute = args =>
                {
                    return functionImplementations.SetExpressionSad();
                }
            });

        registry.Register(new FunctionMeta
            {
                Name = "SetExpressionAngry",
                Description = "设置游戏角色的表情为愤怒",
                Parameters = new Dictionary<string, ParameterMeta> { },
                Execute = args =>
                {
                    return functionImplementations.SetExpressionAngry();
                }
            });

        registry.Register(new FunctionMeta
        {
            Name = "SetExpressionSurprised",
            Description = "设置游戏角色的表情为惊讶",
            Parameters = new Dictionary<string, ParameterMeta> { },
            Execute = args =>
            {
                return functionImplementations.SetExpressionSurprised();
            }
        });

        registry.Register(new FunctionMeta
        {
            Name = "SetExpressionDisgusted",
            Description = "设置游戏角色的表情为厌恶",
            Parameters = new Dictionary<string, ParameterMeta> { },
            Execute = args =>
            {
                return functionImplementations.SetExpressionDisgusted();
            }
        });

    }
}