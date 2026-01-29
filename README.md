<div align="center">
<img src="https://i.imgur.com/7U4LJza.png">
  
# Unity Runtime Voice AI in 20ms
</div>

<p align="center">
  <strong><a href="#en-us">English</a></strong> | <a href="#zh-cn">中文</a>
</p>

<div id="en-us">

[![Unity](https://img.shields.io/badge/Unity-2022.3.62%2B-000000?logo=unity&logoColor=white&color=grey)](https://unity.com/)
![csharp](https://img.shields.io/badge/C%23-239120?&color=grey)
![Android](https://img.shields.io/badge/-000?logo=android&logoColor=fff)
![Windows](https://img.shields.io/badge/Windows-0078D6?logo=windows&logoColor=white&color=black)
![iOS](https://img.shields.io/badge/-000?logo=ios&logoColor=fff)
![macOS](https://img.shields.io/badge/macOS-black.svg)


`VAI (Unity Voice AI Assistant)` is a **lightweight, low-dependency, and API-key based** system for implementing voice control in your Unity projects. It integrates wake word detection, Automatic Speech Recognition (ASR), local 0.1B embedding model intent match and api-based LLM-driven tool calling to translate natural user speech into executable functions. Local test fastest response in **20ms**.

This system can be easily integrated into any Unity project.

A user can say something like, `"Flip the cube, move me a bit closer to it and paint the ball navy blue"` and the system intelligently parses this into a precise, structured command that your application can execute instantly.

<p align="center">
 <img src="https://i.imgur.com/XFF6u5E.gif" width="600">
</p>

<table align="center">
  <tr>
    <td><img src="https://i.imgur.com/MXbp5vp.png" width="350"></td>
    <td><img src="https://i.imgur.com/CuRH15w.png" width="350"></td>
  </tr>
  <tr>
    <td><img src="https://i.imgur.com/VSuOYZx.png" width="350"></td>
    <td><img src="https://i.imgur.com/IjXZdsb.jpeg" width="350"></td>
  </tr>
</table>


## Features

* Customizable Wake Word. [^1]
* **High-Accuracy Multilingual ASR**: Integrates Alibaba Cloud [Paraformer v2](https://help.aliyun.com/zh/model-studio/paraformer-speech-recognition/?spm=a2c4g.11186623.0.i2#undefined) for reliable speech-to-text conversion.
  > 🔔 The paraformer API is only available for registration and use within China. Future updates will support the sherpa-onnx paraformer v1 realtime model.
* **Local Intent Recognition**: Uses a local embedding 0.1B model ([bge-small-zh-v1.5](https://huggingface.co/BAAI/bge-small-zh-v1.5)) for improved performance and speed. Tested to support both Chinese and English.
  > 🔔 The local real-time intent parsing module reduces LLM call frequency, locks the function before LLM, and provides zero-delay feedback.
* **LLM-Powered High-Accuracy Intent Parsing**: Utilizes [Qwen LLM](https://help.aliyun.com/zh/model-studio/use-qwen-by-calling-api?spm=a2c4g.11186623.help-menu-2400256.d_2_1_0.138069ceCqgko9#a9b7b197e2q2v) to interpret natural language and convert it into structured JSON `tool_call`.
* **Seamless Function Execution**: Automatically maps the LLM's output to corresponding Unity functions and executes them.
* **Simple Configuration via UI**: You can use the VAI UI in your own project.

## Performance

| Mode | Response Time Avg.\* | Single-Task Accuracy | Multi-Step Success | Cost** | SUS[^2] Usability |
|:---:|:---:|:---:|:---:|:---:|:---:|
| **VAI (Hybrid)** | **452ms** ██░░░░░░░░ | **89%** | **96%** | **¥7.86** | **78.7** |
| Cloud-Only | 1204ms ████████░░ | 75% | 98% | ¥19.86 | 74.2 |
| Local-Only |   48ms $█░░░░░░░░░$ | 68% | 0% | ¥4.86 | 73.8 |

> \* Time during ASR result and final actions

> \** Cost per 1000 Commands (est.) based on real-world usage with ~80% local intent interception rate vs. cloud-only LLM solutions


## How It Works

The system operates in workflow:

<!--
@startuml

title VAI

actor User as 用户
    participant "VAD (Voice Activity Detection)" as VAD
    participant "ASR (Speech Recognition)" as ASR
    participant "NLU (Intent Matching)" as NLU
    participant "LLM (Large Language Model)" as LLM
    participant "Functions" as Functions

User -> VAD: Starts speaking
activate VAD
note right of VAD: Detects wake word

VAD -> ASR: Streams audio data
activate ASR
activate NLU

VAD -> ASR: Silence detected, ASR ends
deactivate VAD

ASR -> NLU: Continuous intent matching
deactivate ASR

alt Fast Path: Local intent matched

    NLU -> Functions: Directly calls matched function
    activate Functions
  
else Fallback Path: Resort to LLM

    NLU -> LLM: Local intent failed, request LLM
    deactivate NLU
    activate LLM
    LLM -> Functions: tool_call

end

deactivate NLU
deactivate LLM

Functions -> Functions: Executes function call
Functions -> User: Returns operation result
deactivate Functions

@enduml
-->
<p align="center">
 <img src="https://i.imgur.com/DhtR1ku.png" width="600">
</p>

## Getting Started


>🔔Before you begin, ensure you have the following:
> * **Unity Editor**: 2022.3.62 or newer.
> * **Alibaba Cloud Account**: With the BaiLian paraformer and LLM service enabled, we need dashscope api-key.
> For instructions on obtaining a key, see the official documentation: [How to obtain an API Key](https://help.aliyun.com/zh/model-studio/get-api-key)
> * **Aliyun Limitation**: The ASR model using dashscope API is currently only available for registration and use within China. Users outside China cannot register for this service. I plan to adopt the sherpa-onnx paraformer v1 realtime model in future updates, making the system more universally accessible.

### Dependencies
>🔔 Unity should automatically install these dependencies when opening the project. You only need to use NuGet to install `Microsoft.ML.OnnxRuntime`.

1.  **Add OnnxRuntime from [NuGetForUnity](https://github.com/GlitchEnzo/NuGetForUnity)**
    * In `Package Manager`, click the `+` icon and select `Add package from git URL...`.
    * Enter `https://github.com/GlitchEnzo/NuGetForUnity.git?path=/src/NuGetForUnity` and click `Add`.
    * Reopen the project, then in the top menu bar click `NuGet` -> `Manage NuGet Packages`, search for `Microsoft.ML.OnnxRuntime` and click `Install`.

2.  **Add Newtonsoft JSON Package**
    * Add this package from `NuGetForUnity` or in Unity, navigate to `Window` -> `Package Manager`.
    * Click the `+` icon and select `Add package by name...`.
    * Enter `com.unity.nuget.newtonsoft-json` and click `Add`.

3.  **Add [NativeWebSocket](https://github.com/endel/NativeWebSocket) Package**
    * In `Package Manager`, click the `+` icon and select `Add package from git URL...`.
    * Enter `https://github.com/endel/NativeWebSocket.git#upm` and click `Add`.

4. **Download Streaming Assets**
    * Download the [sherpa-onnx realtime asr model and bge-small-zh-v1.5 files](https://pan.baidu.com/s/1QYQMk2LMdgVKprkFQVlj2g?pwd=gujq).
    * Extract files wihtin to `VAI/Assets/StreamingAssets/`.

## Usage

1.  **Open the Example Scene**
    * In your `Project` window, locate to `Assets\VAI\Scenes` and open the `ExampleScene`.

2.  **Configure Wake Word**
    * Select the GameObject containing the voice command script in the `Hierarchy`.
    * In the `Inspector`, find the `Wake Word` field and set your desired activation phrase(s).

3.  **Configure API Key**
    * Set your API Key as an environment variable and fill the environment variable name in the Inspector.
    * The system will automatically read the API Key from the environment variables.

4.  **Register functions at `functionRegistryExample.json`**
    * Before using VAI, please place all the functions you want to use in a single script. Then, reference this function script in the `functionRegistryExample.json`. This will be called by `VAI.LlmController` and `VAI.NluController` in the current scene during `Start()`. You can view all registered functions available for voice control in the UI panel. Please ensure that the function names and parameters strictly match their definitions.
    * There are two functions which are to change transform and color for objects at the scene.

    ```json
    // functionRegistryExample.json
    {
        "Name": "ChangeObjectColor",
        "Description": "Change the color of game object",
        "NameSynonyms": [
            "颜色","color","变成"
        ],
        "Parameters": [
            {
                "ParamName": "objectName",
                "ParamType": "String",
                "EnumValues": [
                    {
                        "Value": "cube",
                        "Keywords": ["正方体", "cube"]
                    }]
            },
            {
                "ParamName": "hexColor",
                "ParamType": "String",
                "EnumValues": [
                    {
                        "Value": "#FF0000",
                        "Keywords": ["red", "red color"]
                    }]
            }
        ]
    }

    ```

5.  **Run the Application**
    * Press the `Play` button in the Unity Editor.
    * State your wake word, followed by a command, to interact with the scene.

6.  **Integration with Your Own Project**
    * Use the `Startup()` function to initialize and start the VAI service and UI.
    * Use the `Shutdown()` function to stop the VAI service and UI when needed.
    * This makes it easy to integrate VAI into your existing Unity projects.

## Known Issues

- `Failed to load 'Assets/.../sherpa-onnx-c-api.dll'` `DllNotFoundException: sherpa-onnx-c-api assembly`
  > This is usually caused by DLL conflicts for different platforms under Plugins. Disable the `Import Settings` -> `Select platforms for plugin` -> `Include Platforms` -> `Editor` for DLLs you are not currently using, and only keep the DLL for the platform you need.

- Android devices cannot use custom wake words
  > Android devices cannot read custom wake words. You must manually modify the wake word file under the app's persistent address on the Android device.

- Android devices cannot use dashscope API environment variables, causing ASR and LLM to fail.
  > Hardcode the environment variable when building the app. Windows does not have this issue. Future updates will add environment variable migration fixes.

- If the system fails, first check if the API is out of quota or if the environment variable is not set.

<br>
<p align="right"><a href="#readme">⬆ Back to Top</a></p>
</div>

<div id="zh-cn">

[](https://unity.com/)
[](https://www.google.com/search?q=%23)

`VAI (Unity Voice AI Assistant)` 是一个端到端全平台流程，用于在您的 Unity 项目中实现语音控制。它集成了唤醒词检测、自动语音识别 (ASR) 、本地 embedding 意图识别和由大语模型 (LLM) 驱动的工具调用，可将用户的自然语言语音转化为可执行的函数。本地测试指令最快在 **20ms 内响应**。

该系统可以非常方便地集成到任何 Unity 项目中。

用户可以说出像“`把正方体回转一下，挪向我这边，然后把球刷成海军蓝色`”这样的指令，系统会智能地将其解析为一个精确、结构化的命令，您的应用程序可以立即执行。

## 功能

  * **自定义唤醒词**：定义一个或多个短语来激活语音监听。[^1]
  * **高精度多语言 ASR**：集成了阿里云的 [Parformer v2](https://help.aliyun.com/zh/model-studio/paraformer-speech-recognition/?spm=a2c4g.11186623.0.i2#undefined)，实现可靠的语音转文本功能。  
    > 🔔paraformer API 仅限中国地区注册使用，后续将支持 sherpa-onnx 的 paraformer v1 实时模型。
  * **本地意图识别**：使用本地 embedding 0.1B 模型 [bge-small-zh-v1.5](https://huggingface.co/BAAI/bge-small-zh-v1.5)，可获得更好的性能和速度。经测试支持中英双语。
    > 🔔通过本地实时的意图解析模块, 可以减少LLM 调用频次, 在LLM 之前锁定函数并实现0延迟反馈。
  * **LLM 驱动的高准确率意图解析**：利用[通义千问大语言模型](https://help.aliyun.com/zh/model-studio/use-qwen-by-calling-api?spm=a2c4g.11186623.help-menu-2400256.d_2_1_0.138069ceCqgko9#a9b7b197e2q2v)来解释自然语言，并将其转换为结构化的 JSON `tool_call`。
  * **无缝的函数执行**：自动将 LLM 的输出映射到 Unity 内部相应的函数并执行。
  * **通过 UI 进行简单配置**：您可以在自己的项目中使用 VAI 的 UI 界面。

## 性能
|       模式      |         平均响应时间\*         |  单任务准确率 | 多步任务成功率 |    成本\**   | 可用性评分[^2] |
| :-----------: | :--------------------: | :-----: | :-----: | :-------: | :------------: |
| **VAI（混合模式）** |**452ms** $██░░░░░░░░$ | **89%** | **96%** | **¥7.86** |    **78.7**    |
|      纯云端      |   1204ms $████████░░$   |   75%   |   98%   |   ¥19.86  |      74.2      |
|      纯本地      | 48ms $█░░░░░░░░░$   |   68%   |    0%   |   ¥4.86   |      73.8      |

> \* 从语音识别完成返回文本，到系统确定执行意图并触发函数的时间间隔

> \** 每千次指令成本（估算），基于实际使用场景测算，采用约80%本地意图拦截率，对比纯云端大模型方案


## 工作原理

该系统的工作流程，分为以下步骤：

<!--
@startuml

title VAI

actor 用户 as User
    participant "VAD (声音活动检测)" as VAD
    participant "ASR (语音识别)" as ASR
    participant "NLU (意图匹配)" as NLU
    participant "LLM (大语言模型)" as LLM
    participant "工具函数" as Functions

User -> VAD: 开始说话
activate VAD
note right of VAD: 检测唤醒词

VAD -> ASR: 流式传输音频数据
activate ASR
activate NLU

VAD -> ASR: 检测到静音 ASR结束
deactivate VAD

ASR -> NLU: 持续进行意图匹配
deactivate ASR

alt 快速路径：本地意图匹配成功

    NLU -> Functions: 直接调用匹配到的函数
activate Functions
  
else 兜底路径：求助于大语言模型

    NLU -> LLM: 本地意图失败，请求LLM
    deactivate NLU
    activate LLM
    LLM -> Functions: tool_call

end

deactivate NLU
deactivate LLM

Functions -> Functions: 执行具体的功能调用
Functions -> User: 返回操作结果
deactivate Functions

@enduml
-->
<p align="center">
 <img src="https://i.imgur.com/DhtR1ku.png" width="600">
</p>

## 开始使用

> 🔔 在开始之前，请确保您已具备以下条件：
>
>   * **Unity 编辑器**：2022.3.47 或更高版本。
>   * **阿里云账户**：并已开通百炼的 Paraformer 和大模型服务，我们需要 dashscope API-Key。
>     关于如何获取 API-Key，请参阅官方文档：[如何获取 API Key](https://help.aliyun.com/zh/model-studio/get-api-key)
>   * **ASR 限制**：ASR 用的 paraformer API 仅限中国地区注册使用，其他地区用户无法注册该服务。后面将采用 sherpa-onnx 的 paraformer v1 实时模型，后续会持续更新，提升系统的全球可用性。

### 依赖项

> 🔔 打开项目时，Unity 应该会自动安装这些依赖项。仅需打开NuGet 安装 `Microsoft.ML.OnnxRuntime`

1.  **从 [NuGetForUnity](https://github.com/GlitchEnzo/NuGetForUnity) 包添加 OnnxRuntime**

      * 在 `Package Manager` 中，点击 `+` 图标，选择 `Add package from git URL...`。
      * 输入 `https://github.com/GlitchEnzo/NuGetForUnity.git?path=/src/NuGetForUnity` 并点击 `Add`。
      * 重开项目，在顶部菜单栏点击`NuGet` -\> `Manage NuGet Packages` 搜索 `Microsoft.ML.OnnxRuntime` 点击`Install`。
      
2.  **添加 Newtonsoft JSON 包**

      * 从`NuGetForUnity` 添加此包或在 Unity 中，导航到 `Window` -\> `Package Manager`。
      * 点击 `+` 图标，选择 `Add package by name...`。
      * 输入 `com.unity.nuget.newtonsoft-json` 并点击 `Add`。

3.  **添加 [NativeWebSocket](https://github.com/endel/NativeWebSocket) 包**

      * 在 `Package Manager` 中，点击 `+` 图标，选择 `Add package from git URL...`。
      * 输入 `https://github.com/endel/NativeWebSocket.git#upm` 并点击 `Add`。

4. **从网盘下载 Streaming Assets**
    * 下载 [sherpa-onnx realtime asr 模型和 bge-small-zh-v1.5 的embedding 模型](https://pan.baidu.com/s/1QYQMk2LMdgVKprkFQVlj2g?pwd=gujq).
    * 把里面的文件解压到 `VAI/Assets/StreamingAssets/`.

## 使用方法

1.  **打开示例场景**

      * 在您的 `Project` 窗口中，定位到 `Assets\VAI\Scenes` 并打开 `ExampleScene`。

2.  **配置唤醒词**

      * 在 `Hierarchy` 面板中选择包含语音命令脚本的 `GameObject`。
      * 在 `Assets\StreamingAssets\CustomKeywords.txt` 写入您想要的激活短语，目前仅限中文，格式为
        ```
        x iǎo zh ù sh ǒu @小助手
        ```
3.  **配置 API Key**

      * 将您的 API Key 设置为环境变量，在 Inspector 面板中填写变量名。
      * 系统会自动从环境变量中读取 API Key。

4.  **在 `FuncRegistryExample` 中配置函数**
      * 在使用 VAI 之前，请将所有需要用到的函数放在一个脚本中，然后在 `functionRegistryExample.json` 中引用该函数脚本。在    `functionRegistryExample.json` 中可以注册函数。其会在当前场景的 `VAI.LlmController` 和 `VAI.NluController` 的 Start 阶段被调用。您可以在 UI 面板上查看所有已注册、可通过语音控制的函数。请注意函数名和参数要与函数定义严格对应。
      * 示例中提供了两个函数，用于更改场景中物体的变换和颜色。
      
    ```json
    // functionRegistryExample.json
    {
        "Name": "ChangeObjectColor",
        "Description": "Change the color of game object",
        "NameSynonyms": [
            "颜色","color","变成"
        ],
        "Parameters": [
            {
                "ParamName": "objectName",
                "ParamType": "String",
                "EnumValues": [
                    {
                        "Value": "cube",
                        "Keywords": ["正方体", "cube"]
                    }]
            },
            {
                "ParamName": "hexColor",
                "ParamType": "String",
                "EnumValues": [
                    {
                        "Value": "#FF0000",
                        "Keywords": ["red", "red color"]
                    }]
            }
        ]
    }
    ```

5.  **运行应用程序**

      * 在 Unity 编辑器中点击 `Play` 按钮。
      * 说出您的唤醒词，然后跟上一个命令，与场景进行交互。

6.  **集成到您自己的项目中**

      * 使用 `Startup()` 函数来初始化并启动 VAI 服务和 UI。
      * 使用 `Shutdown()` 函数在需要时停止 VAI 服务和 UI。
      * 这使得将 VAI 集成到您现有的 Unity 项目中变得非常简单。

## Known Issues

- `Failed to load 'Assets/.../sherpa-onnx-c-api.dll'` `DllNotFoundException: sherpa-onnx-c-api assembly`
  > 这通常是 Plugins 下面的不同平台的dll 冲突，把当前不用的dll 的`Import Settings` -\> `Select platforms for plugin` -\> `Include Platforms` -\> `Editor` 关掉就行，只保留当前要用的平台。

- 安卓设备无法使用自定义唤醒词
  > 安卓设备读不到自定义唤醒词，只能手动在安卓设备的app persistent address 下面的唤醒词文件里面修改。

- 安卓设备无法使用dashscope api 的环境变量，导致ASR和LLM失效。
  > 打包的时候手动写死环境变量。Windows没有这个问题。之后会增加环境变量迁移的修复。

- 失效先检查是否api 欠费或者环境变量没有设置

<br>
<p align="right"><a href="#readme">⬆ 返回顶部</a></p>
</div>


[^1] sherpa-onnx-unity. https://github.com/EitanWong/com.eitan.sherpa-onnx-unity

[^2] An Empirical Evaluation of the System Usability Scale. https://doi.org/10.1080/10447310802205776