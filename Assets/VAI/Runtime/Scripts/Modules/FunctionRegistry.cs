using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using System.Reflection;

namespace VAI
{
    public class JsonFunctionDefinition
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public List<string> NameSynonyms { get; set; }
        public List<JsonParameterDefinition> Parameters { get; set; }
    }
    public class JsonParameterDefinition
    {
        public string ParamName { get; set; }
        public string ParamType { get; set; } // "String", "Number", "Boolean"
        public List<EnumValueDefinition> EnumValues { get; set; }
    }
    public class EnumValueDefinition
    {
        // "Value" is the value that the program recognizes, e.g., "moveleft" or "#FF0000"
        public object Value { get; set; }
        // "Keywords" is the word that the user might say, e.g., ["向左", "left"]
        public List<string> Keywords { get; set; }
    }

    public class FunctionMeta
    {
        public string Name;
        public string Description;
        public Dictionary<string, ParameterMeta> Parameters;
        public Func<IDictionary<string, object>, string> Execute; // unified execution entry
        public List<string> NameSynonyms;
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
        public string Description; // optional
        [Tooltip("Parameter enum (if have)")]
        public Dictionary<string, object> EnumMapping;
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
        public string Name; // parameter name, MUST match the method parameter name in your script
        [Tooltip("Parameter type (string, number, boolean)")]
        public ParamType Type; // "string", "number", "boolean"
        [Tooltip("Parameter description (optional)")]
        public string Description; // optional
        [Tooltip("Parameter enum (if have)")]
        public List<string> Enum; // optional
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

        public void RegisterFunctionsFromJson(TextAsset configFile, object implementations)
        {
            // 1. 从TextAsset读取并反序列化JSON
            List<JsonFunctionDefinition> definitions;
            try
            {
                definitions = JsonConvert.DeserializeObject<List<JsonFunctionDefinition>>(configFile.text);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error parsing function config file '{configFile.name}': {e.Message}");
                return;
            }

            // 2. 遍历每个定义，并将其转换为FunctionMeta进行注册
            foreach (var def in definitions)
            {
                var parameters = new Dictionary<string, ParameterMeta>();
                foreach (var paramDef in def.Parameters)
                {
                    var enumMapping = new Dictionary<string, object>();
                    if (paramDef.EnumValues != null)
                    {
                        foreach (var enumValue in paramDef.EnumValues)
                        {
                            foreach (var keyword in enumValue.Keywords)
                            {
                                if (!enumMapping.ContainsKey(keyword))
                                {
                                    enumMapping[keyword] = enumValue.Value;
                                }
                            }
                        }
                    }

                    parameters[paramDef.ParamName] = new ParameterMeta
                    {
                        Type = (ParameterMeta.ParamType)Enum.Parse(typeof(ParameterMeta.ParamType), paramDef.ParamType, true),
                        EnumMapping = enumMapping
                    };
                }

                // 3. 使用反射动态创建Execute委托
                var executeAction = new Func<IDictionary<string, object>, string>(args =>
                {
                    MethodInfo methodInfo = implementations.GetType().GetMethod(def.Name);
                    if (methodInfo == null) return $"Error: Method '{def.Name}' not found.";

                    ParameterInfo[] methodParams = methodInfo.GetParameters();
                    object[] callArgs = new object[methodParams.Length];

                    for (int i = 0; i < methodParams.Length; i++)
                    {
                        ParameterInfo pInfo = methodParams[i];
                        if (args.TryGetValue(pInfo.Name, out object argValue))
                        {
                            try
                            {
                                callArgs[i] = Convert.ChangeType(argValue, pInfo.ParameterType);
                            }
                            catch (Exception)
                            {
                                return $"Error converting parameter '{pInfo.Name}'.";
                            }
                        }
                        else
                        {
                            return $"Error: Missing argument '{pInfo.Name}'.";
                        }
                    }

                    try
                    {
                        object result = methodInfo.Invoke(implementations, callArgs);
                        return result?.ToString() ?? string.Empty;
                    }
                    catch (Exception ex)
                    {
                        return $"Error during execution of '{def.Name}': {ex.InnerException?.Message ?? ex.Message}";
                    }
                });

                // 4. 创建并注册最终的FunctionMeta对象
                var meta = new FunctionMeta
                {
                    Name = def.Name,
                    Description = def.Description,
                    NameSynonyms = def.NameSynonyms,
                    Parameters = parameters,
                    Execute = executeAction
                };

                this.Register(meta); // 使用 this.Register 或直接 Register
            }

            Debug.Log($"Successfully registered {definitions.Count} functions from '{configFile.name}'.");
        }
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
