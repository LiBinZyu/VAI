using System;
using System.Collections.Generic;
using UnityEngine;

namespace VAI
{
    public class FunctionMeta
    {
        public string Name;
        public string Description;
        public Dictionary<string, ParameterMeta> Parameters;
        public Func<IDictionary<string, object>, string> Execute; // 统一的执行入口
    }

    public class ParameterMeta
    {
        public string Type; // "string", "number", "boolean"
        public string Description;
        public List<string> Enum; // 可选
    }

    public static class FunctionRegistry
    {
        private static Dictionary<string, FunctionMeta> _functions = new();

        static FunctionRegistry()
        {
            Register(new FunctionMeta
            {
                Name = "ModifyTransform",
                Description = "修改物体的变换属性，包括移动、旋转和缩放",
                Parameters = new Dictionary<string, ParameterMeta>
                {
                    { "objectName", new ParameterMeta { Type = "string", Description = "物体的名字", Enum = new List<string> { "cube", "sphere", "capsule", "main camera" } } },
                    { "transformType", new ParameterMeta { Type = "string", Description = "transform的维度", Enum = new List<string> { "moveleft", "moveright", "movebackward", "moveforward", "moveup", "movedown", "pitch", "yaw", "roll", "scale" } } },
                    { "number", new ParameterMeta { Type = "number", Description = "给物体transform某维度改变的数值，不能出现负数，物体只有0.3m大，摄像机只能移动和旋转（镜像）针对摄像机转动的幅度要小，camera离物体2m远，如果操作摄像机请用正常的反方向" } }
                },
                Execute = args =>
                {
                    var objectName = args["objectName"].ToString();
                    var transformType = args["transformType"].ToString();
                    var number = Convert.ToSingle(args["number"]);
                    var funcList = UnityEngine.Object.FindObjectOfType<FuncCallingList>();
                    return funcList.ModifyTransform(objectName, transformType, number);
                }
            });

            Register(new FunctionMeta
            {
                Name = "ChangeObjectColor",
                Description = "改变物体的颜色",
                Parameters = new Dictionary<string, ParameterMeta>
                {
                    { "objectName", new ParameterMeta { Type = "string", Description = "物体的名字", Enum = new List<string> { "cube", "sphere", "capsule", "main camera" } } },
                    { "hexColor", new ParameterMeta { Type = "string", Description = "hex color code" } }
                },
                Execute = args =>
                {
                    var objectName = args["objectName"].ToString();
                    var hexColor = args["hexColor"].ToString();
                    var funcList = UnityEngine.Object.FindObjectOfType<FuncCallingList>();
                    return funcList.ChangeObjectColor(objectName, hexColor);
                }
            });
            // 在这里继续配置函数
        }

        public static void Register(FunctionMeta meta)
        {
            _functions[meta.Name] = meta;
        }

        public static FunctionMeta Get(string name)
        {
            if (_functions.TryGetValue(name, out var meta))
                return meta;
            throw new ArgumentException($"Unregistered function: {name}");
        }

        public static List<FunctionMeta> All() => new List<FunctionMeta>(_functions.Values);
    }
} 