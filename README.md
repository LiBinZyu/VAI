### Unity Voice AI Command

This tool enables voice commands to control your Unity application using LLM. It includes a user interface (UI) for easy and explicit interaction.

1.  **[Wake Word]** 
    The system hears `"Hi there"` and wakes up. **Important:** this is the only function relies on Windows platform.

2.  **[ASR]** 
    `"turn the light on and make it slightly red-ish"`

3. **[LLM Tool Call]** 
    LLM generates a `tool_call` like this based on what we have:

    {
      "tool_name": "set_light_state",
      "parameters": {
        "power_status": "on",
        "color": "#EE8C8C"
      }
    }

4.  **[Function Execution]** 
    The application receives this structure and executes its internal function.


### Dependencies

1.  [**Newtonsoft JSON for Unity**](https://docs.unity3d.com/Packages/com.unity.nuget.newtonsoft-json@3.2/manual/index.html)
    1.  Open your Unity project.
    2.  Go to `Window` -\> `Package Manager`.
    3.  Click the `+` button in the top left corner.
    4.  Select `Add package by name...`.
    5.  Enter `com.unity.nuget.newtonsoft-json` and click `Add`, wait patiently for installing.

2.  [**NativeWebSocket**](https://github.com/endel/NativeWebSocket/tree/upm)
    1.  Open your Unity project.
    2.  Go to `Window` -\> `Package Manager`.
    3.  Click the `+` button in the top left corner.
    4.  Select `Add package from git URL...`.
    5.  Enter `https://github.com/endel/NativeWebSocket/tree/upm` and click `Add`.

### Usage

1.  Import the `Unity Voice AI Command` package into your Unity project.
2.  Open ExampleScene.
3.  Change the wake word in the Inspector panel. It is `Kang Kang` by default, you can add multiple w ake word at the same time.
4.  In the ASR and LLM Inspector panel, you will find a field to enter your Alibaba Cloud BaiLian **API Key**.
    **Important:** You need an Alibaba Cloud BaiLian API key to use this tool. Refer to the following documentation to obtain one: [如何获取API Key\_大模型服务平台百炼(Model Studio)-阿里云帮助中心](https://www.google.com/search?q=https://help.aliyun.com/document_1611175701489758.html)
5.  Click to run.