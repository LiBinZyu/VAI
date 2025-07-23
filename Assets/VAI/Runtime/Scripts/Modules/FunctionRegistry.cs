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

        public List<FunctionMeta> All() => new List<FunctionMeta>(_functions.Values);
    }
}