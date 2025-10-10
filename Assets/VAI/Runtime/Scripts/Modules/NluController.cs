using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using System;
using System.Text.RegularExpressions;
using System.IO;
using System.Threading.Tasks;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace VAI
{
    #region Data Structures (for compatibility with VAI Manager)

    /// <summary>
    /// Represents a fully resolved command ready to be executed.
    /// This structure remains for compatibility with the execution logic.
    /// </summary>
    public class FunctionCallCommand
    {
        public string FunctionName { get; set; }
        public Dictionary<string, object> Arguments { get; set; }

        public FunctionCallCommand()
        {
            Arguments = new Dictionary<string, object>();
        }

        public override string ToString()
        {
            var argsString = Arguments.Any() ? string.Join(", ", Arguments.Select(kvp => $"{kvp.Key}: {kvp.Value}")) : "None";
            return $"[Command Found] Function: {FunctionName}, Args: [{argsString}]";
        }
    }

    #endregion


    #region Main Controller (MonoBehaviour)

    /// <summary>
    /// NluController is the single entry point for Natural Language Understanding.
    /// It uses a BGE ONNX model to perform semantic matching of user queries
    /// to registered functions.
    /// </summary>
    public class NluController : MonoBehaviour
    {
        [Header("BGE Model Configuration")]
        [Tooltip("The JSON file containing function definitions, keywords, and enums.")]
        public TextAsset functionRegistryJson;
        
        [Tooltip("Path to the ONNX model file, relative to StreamingAssets.")]
        public string onnxPath = "bge-small-zh-v1.5-onnx/bge-small-zh-v1.5.onnx";
        
        [Tooltip("Path to the vocab.txt file, relative to StreamingAssets.")]
        public string vocabPath = "bge-small-zh-v1.5-onnx/vocab.txt";
        
        [Tooltip("Path to the tokenizer_config.json file, relative to StreamingAssets.")]
        public string configPath = "bge-small-zh-v1.5-onnx/tokenizer_config.json";

        [Header("BGE Algorithm Tuning")]
        [Range(0, 1)]
        public float FUNC_WEIGHT = 0.6f;
        [Range(0, 1)]
        public float PARAM_WEIGHT = 0.4f;
        [Range(0, 1)]
        public float FIT_SCORE_THRESHOLD = 0.6f;
        [Range(0, 1)]
        public float DISCRIMINATION_MARGIN = 0.1f;
        public string BGE_QUERY_INSTRUCTION = ""; // For BGE-M3 or other instruction-tuned models
        
        [Header("Inspector Testing")]
        [Tooltip("Enter a query here and click 'Execute Test Query' from the component's context menu (three dots).")]
        public string testQuery = "Move the cube left for 2 meters away";
        public bool sendTestQuery = false;


        // Internal State
        private BgeOnnxMatcher _matcher;
        private volatile bool _isInitialized = false;
        private readonly List<FunctionCallCommand> _highConfidenceCommands = new List<FunctionCallCommand>();
        
        void Awake()
        {
            if (functionRegistryJson == null)
            {
                Debug.LogError("Function Registry JSON is not assigned in the NluController.", this);
                return;
            }
            // Start initialization on a background thread to avoid blocking the main thread.
            InitializeAsync();
        }

        void Update()
        {
            if (sendTestQuery)
            {
                TestQueryFromInspector();
                sendTestQuery = false;
            }
        }

        /// <summary>
        /// Initializes the BGE Matcher on a background thread.
        /// </summary>
        private async void InitializeAsync()
        {
            Debug.Log("[NLU] Starting BGE ONNX Matcher initialization...");
            try
            {
                // --- FIX: Read data from the Unity object on the main thread first ---
                string registryJsonText = functionRegistryJson.text;

                await Task.Run(() =>
                {
                    // Construct full paths for the matcher
                    string fullOnnxPath = Path.Combine(Application.streamingAssetsPath, onnxPath);
                    string fullVocabPath = Path.Combine(Application.streamingAssetsPath, vocabPath);
                    string fullConfigPath = Path.Combine(Application.streamingAssetsPath, configPath);

                    // --- FIX: Use the string variable that was passed into the task ---
                    var functionDefinitions = JsonConvert.DeserializeObject<List<JsonFunctionDefinition>>(registryJsonText);

                    // Create the matcher instance
                    _matcher = new BgeOnnxMatcher(
                        fullOnnxPath,
                        functionDefinitions,
                        fullVocabPath,
                        fullConfigPath,
                        FUNC_WEIGHT,
                        PARAM_WEIGHT,
                        FIT_SCORE_THRESHOLD,
                        DISCRIMINATION_MARGIN,
                        BGE_QUERY_INSTRUCTION
                    );
                });

                _isInitialized = true;
                Debug.Log("[NLU] BGE ONNX Matcher initialization complete. Ready to process queries.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[NLU] Failed to initialize BgeOnnxMatcher. Error: {e.Message}\n{e.StackTrace}", this);
            }
        }
        
        #region Public Interface for VAI Manager

        /// <summary>
        /// Processes a sentence from ASR, identifies a high-confidence command, and stores it.
        /// This is the main entry point called by VaiManager.
        /// </summary>
        public void ProcessAsrResult(Sentence asrSentence)
        {
            if (!_isInitialized)
            {
                Debug.LogWarning("[NLU] Matcher not yet initialized. Please wait.");
                return;
            }
            if (asrSentence?.text == null)
            {
                Debug.LogWarning("[NLU] Received null or empty ASR result.");
                return;
            }

            Debug.Log($"[NLU] Processing query: \"{asrSentence.text}\"");
            
            // Analyze the query to get scores for all functions
            var analysisResults = _matcher.AnalyzeQuery(asrSentence.text);
            
            // Make a decision based on the scores and thresholds
            var (isConfident, finalDecision) = _matcher.MakeDecision(analysisResults);
            
            _highConfidenceCommands.Clear(); // Clear previous commands
            
            if (isConfident)
            {
                var command = new FunctionCallCommand
                {
                    FunctionName = finalDecision.FunctionName,
                    Arguments = finalDecision.Arguments
                };
                _highConfidenceCommands.Add(command);
                Debug.Log($"[NLU Stored Command]: {command.ToString()}");
            }
            else
            {
                Debug.Log("[NLU] No high-confidence command found for the query.");
            }
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

        #endregion

        #region Inspector Testing
        
        [ContextMenu("Execute Test Query")]
        private void TestQueryFromInspector()
        {
            if (string.IsNullOrWhiteSpace(testQuery))
            {
                Debug.LogWarning("Test query string is empty. Please enter a query in the Inspector.");
                return;
            }
            TestQuery(testQuery);
        }

        /// <summary>
        /// Runs a full analysis and decision process on a given query string and prints detailed logs.
        /// </summary>
        public void TestQuery(string query)
        {
            if (!_isInitialized)
            {
                Debug.LogError("Matcher is not initialized. Cannot run test query.");
                return;
            }
            
            var startTime = Time.realtimeSinceStartup;

            var analysis = _matcher.AnalyzeQuery(query);

            Debug.Log("<b>--- [TEST] Overall Match Analysis Report ---</b>");
            var analysisString = string.Join(",\n ", analysis.Select(r => r.ToString()));
            Debug.Log($"[\n {analysisString}\n]");

            Debug.Log("\n<b>--- [TEST] Confidence Algorithm Output ---</b>");
            var (isConfident, finalDecision) = _matcher.MakeDecision(analysis);
            var duration = (Time.realtimeSinceStartup - startTime) * 1000;

            Debug.Log($"Is Confident: {isConfident}");
            if (isConfident)
            {
                Debug.Log("Confident Function Call:");
                string args = string.Join(", ", finalDecision.Arguments.Select(kv => $"{kv.Key}: {kv.Value}"));
                Debug.Log($"  -> {finalDecision.FunctionName}({args})");
            }
            else
            {
                Debug.Log("No high-confidence match found.");
            }

            Debug.Log($"\n>> Query Time: {duration:F2} ms");
            Debug.Log("========================================================\n\n");
        }
        
        #endregion
    }

    #endregion

    public class BgeOnnxMatcher
    {
        // --- Algorithm Parameters ---
        private readonly float _funcWeight;
        private readonly float _paramWeight;
        private readonly float _fitScoreThreshold;
        private readonly float _discriminationMargin;
        private readonly string _queryInstruction;

        private readonly InferenceSession _session;
        public readonly BgeTokenizer _tokenizer; // Public for debugging
        private readonly List<JsonFunctionDefinition> _functions;
        private readonly Dictionary<string, float[]> _embeddings = new Dictionary<string, float[]>();

        public BgeOnnxMatcher(string onnxModelPath, List<JsonFunctionDefinition> functions, string vocabPath, string tokenizerConfigPath, 
                              float funcWeight, float paramWeight, float fitScoreThreshold, float discriminationMargin, string queryInstruction)
        {
            _functions = functions;
            _funcWeight = funcWeight;
            _paramWeight = paramWeight;
            _fitScoreThreshold = fitScoreThreshold;
            _discriminationMargin = discriminationMargin;
            _queryInstruction = queryInstruction;

            Debug.Log("1. Loading BGE Tokenizer...");
            _tokenizer = new BgeTokenizer(vocabPath, tokenizerConfigPath);
            Debug.Log("   Tokenizer loaded.");

            Debug.Log($"2. Loading ONNX model from: {onnxModelPath}...");
            var sessionOptions = new SessionOptions { LogSeverityLevel = OrtLoggingLevel.ORT_LOGGING_LEVEL_WARNING };
            _session = new InferenceSession(onnxModelPath, sessionOptions);
            Debug.Log("   ONNX model loaded.");
            
            _precomputeEmbeddings();
        }

        private void _precomputeEmbeddings()
        {
            Debug.Log("3. Pre-computing embeddings for the function library...");
            var textsToEmbed = new HashSet<string>();
            foreach (var func in _functions)
            {
                textsToEmbed.Add(func.Name);
                if (func.NameSynonyms != null)
                {
                    foreach (var synonym in func.NameSynonyms) textsToEmbed.Add(synonym);
                }

                if (func.Parameters == null) continue;

                foreach (var param in func.Parameters)
                {
                    // Note: We don't embed the parameter name itself as it's often not in the spoken query.
                    // We only care about the enum values/keywords that a user would say.
                    if (param.EnumValues != null)
                    {
                        foreach (var enumVal in param.EnumValues)
                        {
                            var keywords = enumVal.Keywords ?? new List<string> { enumVal.Value.ToString() };
                            foreach (var keyword in keywords) textsToEmbed.Add(keyword);
                        }
                    }
                }
            }

            var textList = textsToEmbed.Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
            if (!textList.Any())
            {
                Debug.LogWarning("   Embedding library is empty. No keywords or function names found.");
                return;
            }

            List<float[]> vectorList = Encode(textList);
            for (int i = 0; i < textList.Count; i++)
            {
                _embeddings[textList[i]] = vectorList[i];
            }
            Debug.Log($"   Embedding library pre-computation complete. {_embeddings.Count} items vectorized.");
        }

        public List<AnalysisResult> AnalyzeQuery(string query)
        {
            string queryWithInstruction = _queryInstruction + query;
            float[] queryVector = Encode(new List<string> { queryWithInstruction })[0];
            
            var extractedNumbers = _extractNumbers(query);
            var allFunctionScores = new List<AnalysisResult>();

            foreach (var func in _functions)
            {
                var s_func_candidates = new List<float>();
                
                float[] nameVec = _getEmbedding(func.Name);
                if (nameVec != null) s_func_candidates.Add(CosineSimilarity(queryVector, nameVec));
                
                if(func.NameSynonyms != null)
                {
                    foreach(var synonym in func.NameSynonyms)
                    {
                        float[] synonymVec = _getEmbedding(synonym);
                        if (synonymVec != null) s_func_candidates.Add(CosineSimilarity(queryVector, synonymVec));
                    }
                }

                float s_func = s_func_candidates.Any() ? s_func_candidates.Max() : -1.0f;
                var paramScores = new List<float>();
                var bestParams = new Dictionary<string, object>();

                if (func.Parameters != null)
                {
                    foreach (var param in func.Parameters)
                    {
                        float s_param_best = 0.0f;
                        object best_value = null;

                        if (param.EnumValues != null && param.EnumValues.Any())
                        {
                            float max_enum_score = -1.0f;
                            object best_enum_value = null;

                            foreach (var enumVal in param.EnumValues)
                            {
                                var keywords = enumVal.Keywords ?? new List<string> { enumVal.Value.ToString() };
                                float current_enum_max_score = -1.0f;

                                foreach (var keyword in keywords)
                                {
                                    float[] enumVec = _getEmbedding(keyword);
                                    if (enumVec != null)
                                    {
                                        float score = CosineSimilarity(queryVector, enumVec);
                                        current_enum_max_score = Mathf.Max(current_enum_max_score, score);
                                    }
                                }
                                if (current_enum_max_score > max_enum_score)
                                {
                                    max_enum_score = current_enum_max_score;
                                    best_enum_value = enumVal.Value;
                                }
                            }
                            s_param_best = max_enum_score;
                            best_value = best_enum_value;
                        }
                        else if (param.ParamType.Equals("Number", StringComparison.OrdinalIgnoreCase) && extractedNumbers.Any())
                        {
                            // A simple strategy: if a number is needed, and a number was said,
                            // assign a high confidence and take the first number found.
                            s_param_best = 0.9f; 
                            best_value = float.Parse(extractedNumbers[0]);
                            // A more advanced strategy could remove the number after using it.
                        }
                        
                        paramScores.Add(s_param_best);
                        if (best_value != null)
                        {
                            bestParams[param.ParamName] = best_value;
                        }
                    }
                }
                
                float fit_score;
                float s_params_avg;
                if (func.Parameters == null || !func.Parameters.Any())
                {
                    fit_score = s_func;
                    s_params_avg = 0.0f;
                }
                else
                {
                    s_params_avg = paramScores.Any() ? paramScores.Average() : 0.0f;
                    fit_score = (_funcWeight * s_func) + (_paramWeight * s_params_avg);
                }

                allFunctionScores.Add(new AnalysisResult
                {
                    FunctionName = func.Name,
                    FitScore = fit_score,
                    Details = new AnalysisDetails
                    {
                        SFunc = s_func,
                        SParamsAvg = s_params_avg,
                        BestParamsFound = bestParams
                    }
                });
            }
            
            return allFunctionScores.OrderByDescending(x => x.FitScore).ToList();
        }
        
        public (bool, DecisionResult) MakeDecision(List<AnalysisResult> analysisResults)
        {
            if (analysisResults == null || !analysisResults.Any()) return (false, null);

            var bestMatch = analysisResults[0];
            if (bestMatch.FitScore < _fitScoreThreshold)
            {
                Debug.Log($"Decision Failed: Top function '{bestMatch.FunctionName}' ({bestMatch.FitScore:F4}) is below the confidence threshold of {_fitScoreThreshold}.");
                return (false, null);
            }

            if (analysisResults.Count > 1)
            {
                var secondMatch = analysisResults[1];
                if ((bestMatch.FitScore - secondMatch.FitScore) < _discriminationMargin)
                {
                    Debug.Log($"Decision Failed: Intent is ambiguous. '{bestMatch.FunctionName}' ({bestMatch.FitScore:F4}) is too close to '{secondMatch.FunctionName}' ({secondMatch.FitScore:F4}). Margin is {_discriminationMargin}.");
                    return (false, null);
                }
            }
            
            // Final check: Does the chosen function have all its required parameters filled?
            var funcDef = _functions.First(f => f.Name == bestMatch.FunctionName);
            if (funcDef.Parameters.Count > bestMatch.Details.BestParamsFound.Count)
            {
                 var missingParams = funcDef.Parameters.Select(p => p.ParamName).Except(bestMatch.Details.BestParamsFound.Keys);
                 Debug.Log($"Decision Failed: Function '{bestMatch.FunctionName}' is missing required parameters: [{string.Join(", ", missingParams)}]");
                 return (false, null);
            }

            Debug.Log("Decision Succeeded!");
            return (true, new DecisionResult
            {
                FunctionName = bestMatch.FunctionName,
                Arguments = bestMatch.Details.BestParamsFound
            });
        }
        
        #region ONNX and Vector Math Helpers
        
        public List<float[]> Encode(List<string> sentences)
        {
            var allEmbeddings = new List<float[]>();
            if (!sentences.Any()) return allEmbeddings;

            var inputTensors = sentences.Select(s => _tokenizer.Encode(s)).ToList();
            int maxLength = inputTensors[0].Item1.Length;
            
            var inputIds = new DenseTensor<long>(new Memory<long>(inputTensors.SelectMany(t => t.Item1).ToArray()), new[] { sentences.Count, maxLength });
            var attentionMask = new DenseTensor<long>(new Memory<long>(inputTensors.SelectMany(t => t.Item2).ToArray()), new[] { sentences.Count, maxLength });
            var tokenTypeIds = new DenseTensor<long>(new Memory<long>(inputTensors.SelectMany(t => t.Item3).ToArray()), new[] { sentences.Count, maxLength });

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("input_ids", inputIds),
                NamedOnnxValue.CreateFromTensor("attention_mask", attentionMask),
                NamedOnnxValue.CreateFromTensor("token_type_ids", tokenTypeIds)
            };

            using (var outputs = _session.Run(inputs))
            {
                var lastHiddenState = outputs.FirstOrDefault(o => o.Name == "last_hidden_state")?.AsTensor<float>();
                if (lastHiddenState == null)
                {
                    Debug.LogError("Could not find 'last_hidden_state' in model output.");
                    return allEmbeddings;
                }

                var pooled = _clsPooling(lastHiddenState);
                var normalized = _normalizeEmbeddings(pooled);
                allEmbeddings.AddRange(normalized);
            }
            
            return allEmbeddings;
        }

        private float CosineSimilarity(float[] vecA, float[] vecB)
        {
            if (vecA == null || vecB == null || vecA.Length != vecB.Length) return 0;
            float dotProduct = 0.0f, normA = 0.0f, normB = 0.0f;
            for (int i = 0; i < vecA.Length; i++)
            {
                dotProduct += vecA[i] * vecB[i];
                normA += vecA[i] * vecA[i];
                normB += vecB[i] * vecB[i];
            }
            if (normA == 0 || normB == 0) return 0;
            return dotProduct / (Mathf.Sqrt(normA) * Mathf.Sqrt(normB));
        }
        
        private List<float[]> _clsPooling(Tensor<float> modelOutput)
        {
            var pooledOutput = new List<float[]>();
            int batchSize = modelOutput.Dimensions[0];
            int hiddenSize = modelOutput.Dimensions[2];

            for (int i = 0; i < batchSize; i++)
            {
                var embedding = new float[hiddenSize];
                // Extract the [CLS] token embedding (at index 0)
                for (int j = 0; j < hiddenSize; j++)
                {
                    embedding[j] = modelOutput[i, 0, j];
                }
                pooledOutput.Add(embedding);
            }
            return pooledOutput;
        }

        private List<float[]> _normalizeEmbeddings(List<float[]> embeddings)
        {
            var normalized = new List<float[]>();
            foreach (var vec in embeddings)
            {
                float norm = Mathf.Sqrt(vec.Sum(x => x * x));
                norm = (norm == 0) ? 1e-12f : norm;
                normalized.Add(vec.Select(x => x / norm).ToArray());
            }
            return normalized;
        }

        private float[] _getEmbedding(string text)
        {
            _embeddings.TryGetValue(text, out var embedding);
            return embedding;
        }
        
        private List<string> _extractNumbers(string query)
        {
            return Regex.Matches(query, @"\d+\.?\d*").Cast<Match>().Select(m => m.Value).ToList();
        }
        
        #endregion
    }

    #region BGE Result Data Structures
    public class AnalysisDetails
    {
        public float SFunc { get; set; }
        public float SParamsAvg { get; set; }
        public Dictionary<string, object> BestParamsFound { get; set; }
        public override string ToString()
        {
            var paramsFound = string.Join(", ", BestParamsFound.Select(kvp => $"'{kvp.Key}': '{kvp.Value}'"));
            return $"{{'s_func': {SFunc:F4}, 's_params_avg': {SParamsAvg:F4}, 'best_params_found': {{{paramsFound}}}}}";
        }
    }

    public class AnalysisResult
    {
        public string FunctionName { get; set; }
        public float FitScore { get; set; }
        public AnalysisDetails Details { get; set; }
        public override string ToString()
        {
            return $"{{'function_name': '{FunctionName}', 'fit_score': {FitScore:F4}, 'details': {Details}}}";
        }
    }

    public class DecisionResult
    {
        public string FunctionName { get; set; }
        public Dictionary<string, object> Arguments { get; set; }
    }
    #endregion

    #region BGE Tokenizer
    public class BgeTokenizer
    {
        private readonly Dictionary<string, int> _vocab;
        private readonly int _unkTokenId;
        private readonly int _clsTokenId;
        private readonly int _sepTokenId;
        private readonly int _padTokenId;
        private readonly int _maxLength;
        private readonly BertNormalizer _normalizer;
        private readonly BertPreTokenizer _preTokenizer;
        private readonly WordPieceTokenizer _wordPieceTokenizer;

        public BgeTokenizer(string vocabPath, string tokenizerConfigPath)
        {
            _vocab = LoadVocab(vocabPath);
            _unkTokenId = _vocab["[UNK]"];
            _clsTokenId = _vocab["[CLS]"];
            _sepTokenId = _vocab["[SEP]"];
            _padTokenId = _vocab["[PAD]"];

            var config = JsonConvert.DeserializeObject<Dictionary<string, object>>(File.ReadAllText(tokenizerConfigPath));
            _maxLength = Convert.ToInt32(config["model_max_length"]);
            
            _normalizer = new BertNormalizer();
            _preTokenizer = new BertPreTokenizer();
            _wordPieceTokenizer = new WordPieceTokenizer(_vocab, "[UNK]");
        }

        private Dictionary<string, int> LoadVocab(string path)
        {
            var vocab = new Dictionary<string, int>();
            var lines = File.ReadAllLines(path, Encoding.UTF8);
            for (int i = 0; i < lines.Length; i++) { vocab[lines[i]] = i; }
            return vocab;
        }

        public (long[], long[], long[]) Encode(string text)
        {
            string normalized = _normalizer.Normalize(text);
            List<string> preTokens = _preTokenizer.PreTokenizeText(normalized);
            
            var tokens = new List<string>();
            foreach (var token in preTokens) tokens.AddRange(_wordPieceTokenizer.Encode(token));

            tokens.Insert(0, "[CLS]");
            tokens.Add("[SEP]");

            if (tokens.Count > _maxLength)
            {
                tokens = tokens.Take(_maxLength).ToList();
                tokens[_maxLength - 1] = "[SEP]";
            }
            
            var inputIds = tokens.Select(t => (long)_vocab.GetValueOrDefault(t, _unkTokenId)).ToList();
            var attentionMask = Enumerable.Repeat(1L, inputIds.Count).ToList();
            
            while(inputIds.Count < _maxLength)
            {
                inputIds.Add(_padTokenId);
                attentionMask.Add(0L);
            }

            long[] tokenTypeIds = new long[_maxLength]; // All zeros for BGE

            return (inputIds.ToArray(), attentionMask.ToArray(), tokenTypeIds);
        }

        #region Tokenizer Components
        private class BertNormalizer
        {
            public string Normalize(string text)
            {
                text = text.ToLower();
                text = StripAccents(text);
                text = TokenizeChineseChars(text);
                return text;
            }
            private string TokenizeChineseChars(string text)
            {
                var output = new StringBuilder();
                foreach (char c in text)
                {
                    if (IsChineseChar(c)) output.Append(' ').Append(c).Append(' ');
                    else output.Append(c);
                }
                return Regex.Replace(output.ToString(), @"\s+", " ").Trim();
            }
            private bool IsChineseChar(char c) => (c >= 0x4E00 && c <= 0x9FFF) || (c >= 0x3400 && c <= 0x4DBF);
            private string StripAccents(string text) => Regex.Replace(text.Normalize(NormalizationForm.FormD), @"[\u0300-\u036f]", "");
        }
        private class BertPreTokenizer
        {
            private readonly Regex _pattern = new Regex(@"\S+", RegexOptions.Compiled);
            public List<string> PreTokenizeText(string text) => _pattern.Matches(text).Cast<Match>().Select(m => m.Value).ToList();
        }
        private class WordPieceTokenizer
        {
            private readonly Dictionary<string, int> _vocab;
            private readonly string _unkToken;
            private readonly string _prefix = "##";

            public WordPieceTokenizer(Dictionary<string, int> vocab, string unkToken)
            {
                _vocab = vocab;
                _unkToken = unkToken;
            }

            public List<string> Encode(string token)
            {
                if (_vocab.ContainsKey(token)) return new List<string> { token };

                var outputTokens = new List<string>();
                int start = 0;
                while (start < token.Length)
                {
                    int end = token.Length;
                    string currentSubstring = null;
                    while (end > start)
                    {
                        string sub = token.Substring(start, end - start);
                        if (start > 0) sub = _prefix + sub;
                        if (_vocab.ContainsKey(sub))
                        {
                            currentSubstring = sub;
                            break;
                        }
                        end--;
                    }
                    if (currentSubstring == null) return new List<string> { _unkToken };
                    outputTokens.Add(currentSubstring);
                    start = end;
                }
                return outputTokens;
            }
        }
        #endregion
    }
    #endregion
}