<div align="center">

# Unity Voice AI Command
</div>

<p align="center">
  <strong><a href="#en-us">English</a></strong> | <a href="#zh-cn">中文</a>
</p>

<div id="en-us">

[![Unity Version](https://img.shields.io/badge/Unity-2022.3.47%2B-blue.svg)](https://unity.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows%20-blue.svg)](#)

`VAI (Unity Voice AI Command)` is an end-to-end system for implementing voice control in your Unity projects. It integrates wake word detection, Automatic Speech Recognition (ASR), and LLM-driven tool calling to translate natural user speech into executable functions.

A user can say something like, `"turn the light on and make it slightly red-ish,"` and the system intelligently parses this into a precise, structured command that your application can execute instantly.

![screenshot](https://i.imgur.com/HOJluuQ.png)

## Features

* **Customizable Wake Word**: Define one or more phrases to activate voice listening. (**Note**: This feature currently relies on the Windows platform API.)
* **High-Accuracy multilingual ASR**: Integrates with Alibaba Cloud's Qwen model for reliable speech-to-text transcription.
* **LLM-Powered Intent Parsing**: Leverages a Large Language Model to interpret natural language and convert it into a structured JSON `tool_call`.
* **Seamless Function Execution**: Automatically maps the LLM's output to execute corresponding C# functions within Unity.
* **Simple Configuration with UI**: You can use VAI UI in your own project.

## How It Works

The system operates in a simple, four-step workflow:

1.  **Wake Word Detection**
    * The system listens passively for a predefined wake word (e.g., `"Hi there"`).

2.  **Speech-to-Text (ASR)**
    * Upon activation, the user's voice command is recorded and sent to the cloud for transcription.

3.  **LLM Tool Call Generation**
    * The transcribed text is sent to the LLM, which generates a structured JSON `tool_call` based on the user's intent and predefined functions.

    ```json
    {
      "tool_name": "set_light_state",
      "parameters": {
        "power_status": "on",
        "color": "#EE8C8C"
      }
    }
    ```

4.  **Function Execution**
    * Your Unity application receives this JSON, parses it, and invokes the corresponding local C# function with the provided parameters.

## Getting Started


>🔔Before you begin, ensure you have the following:
> * **Unity Editor**: 2022.3.47 or newer.
> * **Windows**: Required for the current wake word implementation.
> * **Alibaba Cloud Account**: With the BaiLian paraformer and Qwen LLM service enabled, we need dashscope api-key.
> For instructions on obtaining a key, see the official documentation: [How to obtain an API Key](https://help.aliyun.com/zh/model-studio/get-api-key)

### Dependencies
>🔔It should be installed automatically by Unity during opening the project.

1.  **Add Newtonsoft JSON Package**
    * In Unity, navigate to `Window` -> `Package Manager`.
    * Click the `+` icon and select `Add package by name...`.
    * Enter `com.unity.nuget.newtonsoft-json` and click `Add`.

2.  **Add NativeWebSocket Package**
    * In the `Package Manager`, click the `+` icon and select `Add package from git URL...`.
    * Enter `https://github.com/endel/NativeWebSocket/tree/upm` and click `Add`.

## Usage

1.  **Open the Example Scene**
    * In your `Project` window, locate to `Assets\VAI\Scenes` and open the `ExampleScene`.

2.  **Configure Wake Word**
    * Select the GameObject containing the voice command script in the `Hierarchy`.
    * In the `Inspector`, find the `Wake Word` field and set your desired activation phrase(s).

3.  **Configure API Key**
    * In the `Inspector`, locate the `ASR and LLM` configuration section.
    * Enter your API Key in the `Api Key` field.

4.  **Write and configer functions at `apiFuncCalling.cs` and `FuncCallingLists.cs`**
    * There are two functions which are to change transform and color for objects at the scene.

5.  **Run the Application**
    * Press the `Play` button in the Unity Editor.
    * State your wake word, followed by a command, to interact with the scene.

<br>
<p align="right"><a href="#readme">⬆ Back to Top</a></p>
</div>

<div id="zh-cn">

[](https://unity.com/)
[](https://www.google.com/search?q=%23)

`VAI (Unity Voice AI Command)` 是一个端到端系统，用于在您的 Unity 项目中实现语音控制。它集成了唤醒词检测、自动语音识别 (ASR) 和由大语言模型 (LLM) 驱动的工具调用，可将用户的自然语言语音转化为可执行的函数。

用户可以说出像“`打开灯，让它变成微红色`”这样的指令，系统会智能地将其解析为一个精确、结构化的命令，您的应用程序可以立即执行。

## 功能

  * **自定义唤醒词**：定义一个或多个短语来激活语音监听。（**注意**：此功能目前依赖于 Windows 平台 API。）
  * **高精度多语言 ASR**：集成了阿里云的通义千问模型，实现可靠的语音转文本功能。
  * **LLM 驱动的意图解析**：利用大语言模型来解释自然语言，并将其转换为结构化的 JSON `tool_call`。
  * **无缝的函数执行**：自动将 LLM 的输出映射到 Unity 内部相应的函数并执行。
  * **通过 UI 进行简单配置**：您可以在自己的项目中使用 VAI 的 UI 界面。

## 工作原理

该系统的工作流程简单，分为四个步骤：

1.  **唤醒词检测**

      * 系统被动监听预设的唤醒词（例如，“`Hi there`”）。

2.  **语音转文本 (ASR)**

      * 激活后，用户的语音命令被录制并发送到云端进行转录。

3.  **LLM 生成工具调用**

      * 转录后的文本被发送到 LLM，LLM 会根据用户的意图和预定义的函数生成一个结构化的 JSON `tool_call`。

    <!-- end list -->

    ```json
    {
      "tool_name": "set_light_state",
      "parameters": {
        "power_status": "on",
        "color": "#EE8C8C"
      }
    }
    ```

4.  **函数执行**

      * 您的 Unity 应用程序接收此 JSON，对其进行解析，并使用提供的参数调用相应的本地函数。

## 开始使用

> 🔔 在开始之前，请确保您已具备以下条件：
>
>   * **Unity 编辑器**：2022.3.47 或更高版本。
>   * **Windows**：当前的唤醒词实现需要此平台。
>   * **阿里云账户**：并已开通百炼的 Paraformer 和Qwen LLM 服务，我们需要 dashscope 的 API-Key。
>     关于如何获取 API-Key，请参阅官方文档：[如何获取 API Key](https://help.aliyun.com/zh/model-studio/get-api-key)

### 依赖项

> 🔔 打开项目时，Unity 应该会自动安装这些依赖项。

1.  **添加 Newtonsoft JSON 包**

      * 在 Unity 中，导航到 `Window` -\> `Package Manager`。
      * 点击 `+` 图标，选择 `Add package by name...`。
      * 输入 `com.unity.nuget.newtonsoft-json` 并点击 `Add`。

2.  **添加 NativeWebSocket 包**

      * 在 `Package Manager` 中，点击 `+` 图标，选择 `Add package from git URL...`。
      * 输入 `https://github.com/endel/NativeWebSocket/tree/upm` 并点击 `Add`。

## 使用方法

1.  **打开示例场景**

      * 在您的 `Project` 窗口中，定位到 `Assets\VAI\Scenes` 并打开 `ExampleScene`。

2.  **配置唤醒词**

      * 在 `Hierarchy` 面板中选择包含语音命令脚本的 `GameObject`。
      * 在 `Inspector` 面板中，找到 `Wake Word` 字段并设置您想要的激活短语。

3.  **配置 API Key**

      * 在 `Inspector` 面板中，找到 `ASR and LLM` 配置部分。
      * 在 `Api Key` 字段中输入您的 API Key。

4.  **在 `apiFuncCalling.cs` 和 `FuncCallingLists.cs` 中编写和配置函数**

      * 示例中提供了两个函数，用于更改场景中物体的变换和颜色。

5.  **运行应用程序**

      * 在 Unity 编辑器中点击 `Play` 按钮。
      * 说出您的唤醒词，然后跟上一个命令，与场景进行交互。

<br>
<p align="right"><a href="#readme">⬆ 返回顶部</a></p>
</div>