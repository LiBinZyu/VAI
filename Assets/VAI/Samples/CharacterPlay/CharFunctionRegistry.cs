using UnityEngine;
using VAI;
using System;
using System.Collections.Generic;
using UnityEngine.UI;
using System.Reflection;

// 这个组件负责为当前场景向LlmController注册特定的函数
public class CharFunctionRegistry : MonoBehaviour
{
    //public LlmController llmController;
    public CharFuncCallingList functionImplementations;
    [Header("Configuration")]
    [Tooltip("Json file that contains the function definitions for NLU and LLM")]
    public TextAsset functionConfigFile;

    [Header("Optional")]
    [Tooltip("You can either register functions in the inspector or in the json")]
    public List<SerializableFunctionMeta> userRegisteredFunctions;
    [Tooltip("Optional, display all functions in the inspector")]
    public Text functionListText;

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

        // clear all functions registered by this script at last time
        LlmController.Instance.ClearFunctionRegistry();
        
        // register functions from json file, recommended
        if (functionConfigFile != null)
        {
            LlmController.Instance.functionRegistry.RegisterFunctionsFromJson(functionConfigFile, functionImplementations);
        }
        else
        {
            // Legacy way to register functions in this script, not recommended
            RegisterSceneFunctions();
        }

        // update and show function list in runtime UI
        if (functionListText)
        {
            functionListText.text = LlmController.Instance.functionRegistry.GetAllFunctionsAsFormattedString();
        }
    }

    void OnDisable()
    {
        if (LlmController.Instance != null)
        {
            // clear all functions registered by this script at last time
            LlmController.Instance.ClearFunctionRegistry();
            Debug.Log($"Functions from {gameObject.name} have been unregistered.", this);
        }
    }

    private void RegisterSceneFunctions()
    {
        var registry = LlmController.Instance.functionRegistry;
        
    }
    
}
