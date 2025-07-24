using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Text;

namespace VAI
{
    public class FunctionMeta
    {
        public string Name;
        public string Description;
        public Dictionary<string, ParameterMeta> Parameters;
        public Func<IDictionary<string, object>, string> Execute; // 统一的执行入口
    }

    [Serializable]
    public class SerializableFunctionMeta
    {
        [Tooltip("Function name (must exist in your function call script))")]
        public string Name;
        [Tooltip("Function description (optional)")]
        public string Description;
        public List<SerializableParameterMeta> Parameters;
    }

    public class ParameterMeta
    {
        public enum ParamType
        {
            String,
            Number,
            Boolean
        }
        [Tooltip("Parameter type (string, number, boolean)")]
        public ParamType Type; // "string", "number", "boolean"
        [Tooltip("Parameter description (optional)")]
        public string Description; // 参数的描述，可选
        [Tooltip("Parameter enum (if have)")]
        public List<string> Enum; // 可选
    }

    [Serializable]
    public class SerializableParameterMeta
    {
        public enum ParamType
        {
            String,
            Number,
            Boolean
        }

        [Tooltip("Parameter name (must match the method parameter name in your script)")]
        public string Name; // 参数的名字，必须与代码中方法的参数名一致
        [Tooltip("Parameter type (string, number, boolean)")]
        public ParamType Type; // "string", "number", "boolean"
        [Tooltip("Parameter description (optional)")]
        public string Description; // 参数的描述，可选
        [Tooltip("Parameter enum (if have)")]
        public List<string> Enum; // 可选
    }


    public class FunctionRegistry
    {
        private Dictionary<string, FunctionMeta> _functions = new();

        // The constructor is now empty. It only prepares the dictionary.
        public FunctionRegistry()
        {
            _functions = new Dictionary<string, FunctionMeta>();
        }

        public void Register(FunctionMeta meta)
        {
            _functions[meta.Name] = meta;
            Debug.Log($"Function registered: {meta.Name}");
        }

        public FunctionMeta Get(string name)
        {
            if (_functions.TryGetValue(name, out var meta))
                return meta;
            throw new ArgumentException($"Unregistered function: {name}");
        }
        public void Clear()
        {
            _functions.Clear();
            Debug.Log("Function registry cleared.");
        }

        public List<FunctionMeta> All() => new List<FunctionMeta>(_functions.Values);

        public string GetAllFunctionsAsFormattedString()
        {
            if (_functions.Count == 0)
            {
                return "No functions registered.";
            }

            var stringBuilder = new StringBuilder();

            foreach (var funcMeta in _functions.Values)
            {
                stringBuilder.AppendLine(funcMeta.Name);
                if (funcMeta.Parameters != null && funcMeta.Parameters.Count > 0)
                {
                    var paramNames = funcMeta.Parameters.Keys.ToArray();
                    stringBuilder.Append("<size=12>");
                    stringBuilder.Append(string.Join(", ", paramNames));
                    stringBuilder.AppendLine("</size>");
                }
                stringBuilder.AppendLine();
            }

            return stringBuilder.ToString();
        }
    }
}