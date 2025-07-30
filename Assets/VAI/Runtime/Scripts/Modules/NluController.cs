using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using System;
using JiebaNet.Segmenter;
using System.Text.RegularExpressions;
using System.Globalization;

//  Namespace is optional but recommended for consistency with your project structure.
namespace VAI
{

    #region Data Transfer Objects (DTOs) for JSON Deserialization

    /// <summary>
    /// Root object for deserializing the mock ASR (Paraformer) response.
    /// This is a pure C# DTO.
    /// </summary>
    public class ParaformerResponse
    {
        public ResponseHeader header { get; set; }
        public ResponsePayload payload { get; set; }

        // Nested DTOs to match the Paraformer JSON structure
        public class ResponseHeader { public string @event; public string task_id; }
        public class ResponsePayload { public Output output; }
        public class Output { public Sentence sentence; }
        public class Sentence { public string text; public Word[] words; }
        public class Word { public string text; public string punctuation; }
    }

    #endregion


    #region NLU Core Logic & Data Structures

    /// <summary>
    /// Represents a fully resolved command ready to be executed.
    /// </summary>
    public class FunctionCallCommand
    {
        public string FunctionName { get; set; }
        public Dictionary<string, object> Arguments { get; set; }
        public float Confidence { get; set; }
        public List<string> MatchedKeywords { get; set; }

        public FunctionCallCommand()
        {
            Arguments = new Dictionary<string, object>();
            MatchedKeywords = new List<string>();
        }

        public override string ToString()
        {
            var argsString = string.Join(", ", Arguments.Select(kvp => $"{kvp.Key}: {kvp.Value}"));
            var keywordsString = string.Join(", ", MatchedKeywords);
            return $"[Command Found] Function: {FunctionName}, Args: [{argsString}], Confidence: {Confidence:F2}, Matched: [{keywordsString}]";
        }
    }


    /// <summary>
    /// Contains all static, reusable NLU algorithms and data structures,
    /// completely decoupled from Unity's MonoBehaviour lifecycle.
    /// </summary>
    public static class NluUtils
    {
        /// <summary>
        /// Internal data structure stored in the Trie to link a keyword back to its origin.
        /// </summary>
        public class KeywordInfo
        {
            public string Keyword { get; }
            public string FunctionName { get; }
            public string ParameterName { get; } // Null if it's a function synonym
            public object MappedValue { get; }   // The actual value for a parameter (e.g., "#FF0000")

            public KeywordInfo(string keyword, string functionName, string parameterName, object mappedValue)
            {
                Keyword = keyword;
                FunctionName = functionName;
                ParameterName = parameterName;
                MappedValue = mappedValue;
            }
        }

        /// <summary>
        /// A standard Trie (Prefix Tree) implementation for efficient keyword searching.
        /// </summary>
        public class Trie
        {
            private class TrieNode
            {
                public Dictionary<char, TrieNode> Children { get; } = new Dictionary<char, TrieNode>();
                public List<KeywordInfo> KeywordInfos { get; } = new List<KeywordInfo>();
            }

            private readonly TrieNode _root = new TrieNode();

            public void Insert(KeywordInfo info)
            {
                var node = _root;
                foreach (char c in info.Keyword)
                {
                    if (!node.Children.ContainsKey(c))
                    {
                        node.Children[c] = new TrieNode();
                    }
                    node = node.Children[c];
                }
                node.KeywordInfos.Add(info);
            }

            public List<KeywordInfo> Search(string phrase)
            {
                var node = _root;
                foreach (char c in phrase)
                {
                    if (!node.Children.TryGetValue(c, out node))
                    {
                        return new List<KeywordInfo>(); // Return empty list if no match
                    }
                }
                return node.KeywordInfos; // Returns list of all keywords that end here
            }
        }
    }
    #endregion


    #region Main Controller (MonoBehaviour)

    /// <summary>
    /// NluController is the single entry point for Natural Language Understanding.
    /// It takes ASR results, processes them, and identifies function calls.
    /// </summary>
    public class NluController : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("The JSON file containing function definitions, keywords, and enums.")]
        public TextAsset functionRegistryJson;

        [Tooltip("NLU command limit")]
        public int HighConfidenceCommandLimit = 2;

        [Header("Offline Debugging")]
        [Tooltip("Mock ASR output for testing without a live ASR feed. Click the 'Parse Mock Data' button in the Inspector to use this.")]
        public ParaformerResponse paraformerMock;

        // Internal State
        private NluUtils.Trie _keywordTrie;
        private List<JsonFunctionDefinition> _functionDefinitions;
        private readonly List<FunctionCallCommand> _highConfidenceCommands = new List<FunctionCallCommand>();
        private JiebaSegmenterHelper _jiebaHelper;

        void Awake()
        {
            if (functionRegistryJson == null)
            {
                Debug.LogError("Function Registry JSON is not assigned in the NluController.", this);
                return;
            }

            try
            {
                // Jieba.Net 配置文件的位置
                JiebaNet.Segmenter.ConfigManager.ConfigFileBaseDir = Application.dataPath + @"/VAI/Plugins/Resources";
                _jiebaHelper = new JiebaSegmenterHelper();
                Debug.Log("JiebaSegmenterHelper initialized successfully.");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to initialize JiebaSegmenterHelper. Make sure the config files are in 'Assets/VAI/Plugins/Resources'. Error: {e.Message}", this);
            }

            BuildTrieFromRegistry();
        }

        private void BuildTrieFromRegistry()
        {
            try
            {
                _functionDefinitions = JsonConvert.DeserializeObject<List<JsonFunctionDefinition>>(functionRegistryJson.text);
                _keywordTrie = new NluUtils.Trie();

                foreach (var funcDef in _functionDefinitions)
                {
                    if (funcDef.NameSynonyms != null)
                    {
                        foreach (var synonym in funcDef.NameSynonyms)
                        {
                            var info = new NluUtils.KeywordInfo(synonym, funcDef.Name, null, null);
                            _keywordTrie.Insert(info);
                        }
                    }
                    foreach (var paramDef in funcDef.Parameters)
                    {
                        if (paramDef.EnumValues != null)
                        {
                            foreach (var enumValue in paramDef.EnumValues)
                            {
                                foreach (var keyword in enumValue.Keywords)
                                {
                                    var info = new NluUtils.KeywordInfo(keyword, funcDef.Name, paramDef.ParamName, enumValue.Value);
                                    _keywordTrie.Insert(info);
                                }
                            }
                        }
                    }
                }
                Debug.Log($"NLU Trie built successfully with {_functionDefinitions.Count} function definitions.");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to parse Function Registry JSON or build Trie. Error: {e.Message}", this);
            }
        }

        /// <summary>
        /// Processes a sentence from ASR, identifies high-confidence commands, and stores them.
        /// This is the new main entry point called by VaiManager.
        /// </summary>
        public void ProcessAsrResult(Sentence asrSentence)
        {
            if (_keywordTrie == null || _jiebaHelper == null || asrSentence?.text == null)
            {
                Debug.LogWarning("[NLU] ProcessAsrResult: Not ready or nothing to process.");
                return;
            }

            // 1. 分词
            var segments = _jiebaHelper.Cut(asrSentence.text);
            Debug.Log($"[NLU Jieba Segments]: {string.Join(" / ", segments)}");

            // 2. 首先，通过函数名同义词，找出所有可能被提及的指令
            var potentialCommands = new List<FunctionCallCommand>();
            foreach (var segment in segments)
            {
                var functionNameMatches = _keywordTrie.Search(segment)
                    .Where(info => info.ParameterName == null) // 只找函数名同义词
                    .Select(info => info.FunctionName)
                    .Distinct();

                foreach (var funcName in functionNameMatches)
                {
                    // 防止重复添加同一个指令
                    if (!potentialCommands.Any(c => c.FunctionName == funcName))
                    {
                        var command = new FunctionCallCommand { FunctionName = funcName };
                        command.MatchedKeywords.Add(segment); // 记录匹配到的函数名关键词
                        potentialCommands.Add(command);
                        Debug.Log($"[NLU] Potential command found: '{funcName}' based on keyword '{segment}'.");
                    }
                }
            }

            if (!potentialCommands.Any())
            {
                Debug.Log("[NLU] No potential commands found based on function name synonyms.");
                return;
            }

            // 3. 遍历所有分词，为已找到的指令填充参数（包括枚举关键词和数字）
            foreach (var segment in segments)
            {
                // a. 尝试将分词作为枚举参数关键词进行匹配
                var paramMatches = _keywordTrie.Search(segment).Where(info => info.ParameterName != null);
                foreach (var match in paramMatches)
                {
                    // 找到这个参数属于哪个指令
                    var targetCommand = potentialCommands.FirstOrDefault(c => c.FunctionName == match.FunctionName);
                    if (targetCommand != null && !targetCommand.Arguments.ContainsKey(match.ParameterName))
                    {
                        targetCommand.Arguments[match.ParameterName] = match.MappedValue;
                        targetCommand.MatchedKeywords.Add(segment);
                        Debug.Log($"[NLU] Matched enum param '{match.ParameterName}' with value '{match.MappedValue}' for command '{targetCommand.FunctionName}'.");
                    }
                }

                // b. 尝试将分词解析为数字并填充
                if (float.TryParse(segment, NumberStyles.Any, CultureInfo.InvariantCulture, out float parsedNumber))
                {
                    // 遍历所有可能的指令，为这个数字找到一个家
                    foreach (var command in potentialCommands)
                    {
                        var funcDef = _functionDefinitions.First(f => f.Name == command.FunctionName);
                        // 查找该函数中第一个类型为'Number'且尚未被赋值的参数
                        var numberParam = funcDef.Parameters.FirstOrDefault(p =>
                            p.ParamType.Equals("Number", StringComparison.OrdinalIgnoreCase) &&
                            !command.Arguments.ContainsKey(p.ParamName));

                        if (numberParam != null)
                        {
                            command.Arguments[numberParam.ParamName] = parsedNumber;
                            command.MatchedKeywords.Add(segment);
                            Debug.Log($"[NLU] Matched number '{segment}' to param '{numberParam.ParamName}' for command '{command.FunctionName}'.");
                            break; // 一个数字只分配给一个指令的一个参数
                        }
                    }
                }
            }

            // 4. 最后，检查哪些指令的所有必需参数都已填满，作为高置信度指令存储
            _highConfidenceCommands.Clear();
            foreach (var command in potentialCommands)
            {
                if (_highConfidenceCommands.Count >= HighConfidenceCommandLimit)
                {
                    Debug.LogWarning($"[NLU] Reached command limit ({HighConfidenceCommandLimit}).");
                    break;
                }

                var funcDef = _functionDefinitions.First(f => f.Name == command.FunctionName);

                // 检查是否所有在JSON中定义的参数都找到了值
                if (command.Arguments.Count >= funcDef.Parameters.Count)
                {
                    command.Confidence = 1.0f;
                    _highConfidenceCommands.Add(command);
                    // 这个日志现在应该可以正确打印了！
                    Debug.Log($"[NLU Stored Command]: {command.ToString()}");
                }
                else
                {
                    var missingParams = funcDef.Parameters.Select(p => p.ParamName).Except(command.Arguments.Keys);
                    Debug.LogWarning($"[NLU] Incomplete command '{command.FunctionName}'. Matched {command.Arguments.Count}/{funcDef.Parameters.Count} params. Missing: [{string.Join(", ", missingParams)}]");
                }
            }
        }

        /// <summary>
        ///  <-- 新增：辅助方法，用于判断字符串是否为数字
        /// </summary>
        private bool IsNumeric(string value)
        {
            // 使用float.TryParse，因为它可以同时处理整数和浮点数
            return float.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out _);
        }

        public bool HasStoredCommands()
        {
            return _highConfidenceCommands.Count > 0;
        }

        public string ExecuteStoredCommands()
        {
            if (!HasStoredCommands()) return "没有指令被执行。";

            var registry = LlmController.Instance.functionRegistry;
            var results = new StringBuilder();

            Debug.Log($"--- [NLU] Executing {_highConfidenceCommands.Count} Stored Command(s) ---");

            foreach (var command in _highConfidenceCommands)
            {
                try
                {
                    FunctionMeta funcMeta = registry.Get(command.FunctionName);
                    string result = funcMeta.Execute(command.Arguments);
                    results.AppendLine($"✓ {result}");
                    Debug.Log($"[NLU Execution Result] '{command.FunctionName}': {result}");
                }
                catch (Exception e)
                {
                    string errorMsg = $"执行指令 '{command.FunctionName}' 失败: {e.Message}";
                    results.AppendLine($"✗ {errorMsg}");
                    Debug.LogError(errorMsg);
                }
            }

            ClearStoredCommands(); // Clear after execution
            return results.ToString();
        }

        public void ClearStoredCommands()
        {
            _highConfidenceCommands.Clear();
        }
    }

    #endregion

    public class JiebaSegmenterHelper
    {
        private readonly JiebaSegmenter segmenter;
        private static readonly Regex NumberRegex = new Regex(@"([+\-]?\d+(\.\d+)?)|([一二三四五六七八九十百千万亿零点]+)", RegexOptions.Compiled);

        public JiebaSegmenterHelper()
        {
            segmenter = new JiebaSegmenter();
        }

        public List<string> Cut(string text)
        {
            var placeholders = new Dictionary<string, string>();
            int index = 0;

            string processedText = NumberRegex.Replace(text, match =>
            {
                string placeholder = $"__NUM_{index}__";
                segmenter.AddWord(placeholder);
                placeholders[placeholder] = match.Value;
                index++;
                return placeholder;
            });

            var segments = segmenter.Cut(processedText);

            var finalResult = new List<string>();
            foreach (var segment in segments)
            {
                if (placeholders.ContainsKey(segment))
                {
                    finalResult.Add(placeholders[segment]);
                }
                else
                {
                    finalResult.Add(segment);
                }
            }

            return finalResult;
        }
    }
}
