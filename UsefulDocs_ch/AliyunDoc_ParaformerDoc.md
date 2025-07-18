本文介绍如何通过WebSocket连接访问实时语音识别服务。

DashScope SDK目前仅支持Java和Python。若想使用其他编程语言开发Paraformer实时语音识别应用程序，可以通过WebSocket连接与服务进行通信。

WebSocket是一种支持全双工通信的网络协议。客户端和服务器通过一次握手建立持久连接，双方可以互相主动推送数据，因此在实时性和效率方面具有显著优势。

对于常用编程语言，有许多现成的WebSocket库和示例可供参考，例如：

*   Go：`gorilla/websocket`
    
*   PHP：`Ratchet`
    
*   Node.js：`ws`
    

建议您先了解WebSocket的基本原理和技术细节，再参照本文进行开发。

**前提条件**
--------

已开通服务并[获取API Key](https://help.aliyun.com/zh/model-studio/get-api-key)。请[配置API Key到环境变量](https://help.aliyun.com/zh/model-studio/configure-api-key-through-environment-variables)，而非硬编码在代码中，防范因代码泄露导致的安全风险。

**说明**

当您需要为第三方应用或用户提供临时访问权限，或者希望严格控制敏感数据访问、删除等高风险操作时，建议使用[临时鉴权Token](https://help.aliyun.com/zh/model-studio/obtain-temporary-authentication-token)。

与长期有效的 API Key 相比，临时鉴权 Token 具备时效性短（60秒）、安全性高的特点，适用于临时调用场景，能有效降低API Key泄露的风险。

使用方式：在代码中，将原本用于鉴权的 API Key 替换为获取到的临时鉴权 Token 即可。

**约束**
------

**接口调用方式限制**：不支持前端直接调用API，需通过后端中转。

**模型列表**
--------

**模型名**

**模型简介**

**模型名**

**模型简介**

paraformer-realtime-v2

**推荐使用**。Paraformer最新多语种实时语音识别模型。

*   适用场景：直播、会议等实时语音处理场景
    
*   支持的采样率：任意
    
*   支持的语种：中文（包含中文普通话和各种方言）、英文、日语、韩语、德语、法语、俄语
    
    **支持的中文方言（单击查看详情）**
    
    上海话、吴语、闽南语、东北话、甘肃话、贵州话、河南话、湖北话、湖南话、江西话、宁夏话、山西话、陕西话、山东话、四川话、天津话、云南话、粤语
    
*   核心能力：
    
    *   支持标点符号预测
        
    *   支持逆文本正则化（ITN）
        
*   特色功能：
    
    *   指定语种：通过`language_hints`参数能够指定待识别语种，提升识别效果
        
    *   支持[定制热词](https://help.aliyun.com/zh/model-studio/custom-hot-words)
        

paraformer-realtime-8k-v2

**推荐使用**。Paraformer最新8k中文实时语音识别模型，模型结构升级，具有更快的推理速度和更好的识别效果。

*   适用场景：电话客服、语音信箱等 8kHz 音频的实时识别，需快速推理与高准确率的场景（如实时字幕生成）等
    
*   支持的采样率：8kHz
    
*   支持的语种：中文
    
*   核心能力：
    
    *   支持标点符号预测
        
    *   支持逆文本正则化（ITN）
        

*   特色功能：
    
    *   支持[定制热词](https://help.aliyun.com/zh/model-studio/custom-hot-words)
        
    *   支持情感识别
        
        情感识别仅在语义断句关闭时生效。语义断句默认为关闭状态，可通过[run-task指令](#12d8a57443dmz)的`semantic_punctuation_enabled`参数控制。
        
        句子情感通过解析[result-generated事件](#e9420a4d7bock)获取，具体步骤如下：
        
        1.  获取`payload.output.sentence.sentence_end`的值，只有为true时才能进行下一步。
            
        2.  通过`payload.output.sentence.emo_tag`和`payload.output.sentence.emo_confidence`字段分别获取当前句子的情感和情感置信度。
            

paraformer-realtime-v1

Paraformer中文实时语音识别模型。

*   适用场景：视频直播、会议等实时场景
    
*   支持的采样率：16kHz
    
*   支持的语种：中文
    
*   核心能力：
    
    *   支持标点符号预测
        
    *   支持逆文本正则化（ITN）
        
*   特色功能：
    
    *   支持定制热词（参见[Paraformer语音识别热词定制与管理](https://help.aliyun.com/zh/model-studio/developer-reference/paraformer-asr-phrase-manager)）
        

paraformer-realtime-8k-v1

Paraformer中文实时语音识别模型。

*   适用场景：8kHz电话客服等场景
    
*   支持的采样率：8kHz
    
*   支持的语种：中文
    
*   核心能力：
    
    *   支持标点符号预测
        
    *   支持逆文本正则化（ITN）
        
*   特色功能：
    
    *   支持定制热词（参见[Paraformer语音识别热词定制与管理](https://help.aliyun.com/zh/model-studio/developer-reference/paraformer-asr-phrase-manager)）
        

**交互流程**
--------

![image](https://help-static-aliyun-doc.aliyuncs.com/assets/img/zh-CN/3583329471/CAEQURiBgMCczta5pxkiIGY0N2Q2YjIwZTM1MTQyNTY4ZmFkY2MwN2JmOTllODFl4709861_20241015153444.149.svg)

客户端发送给服务端的消息有两种：JSON格式的[指令](#271eb7a50ft6r)和[二进制音频](#e14da5dfe9npt)（须为单声道音频）；服务端返回给客户端的消息称作[事件](#a989eb7099wjv)。

按时间顺序，客户端与服务端的交互流程如下：

1.  建立连接：客户端与服务端建立WebSocket连接。
    
2.  开启任务：
    
    *   客户端发送[run-task指令](#12d8a57443dmz)以开启任务。
        
    *   客户端收到服务端返回的[task-started事件](#2942cede42z9e)，标志着任务已成功开启，可以进行后续步骤。
        
3.  发送音频流：
    
    *   客户端开始发送[二进制音频](#e14da5dfe9npt)，并同时接收服务端持续返回的[result-generated事件](#e9420a4d7bock)，该事件包含语音识别结果。
        
4.  通知服务端结束任务：
    
    *   客户端发送[finish-task指令](#2e967d2d349es)通知服务端结束任务，并继续接收服务端返回的[result-generated事件](#e9420a4d7bock)。
        
5.  任务结束：
    
    *   客户端收到服务端返回的[task-finished事件](#f11a341732xug)，标志着任务结束。
        
6.  关闭连接：客户端关闭WebSocket连接。
    

**URL**
-------

WebSocket URL固定如下：

    wss://dashscope.aliyuncs.com/api-ws/v1/inference

**Headers**
-----------

请求头中需添加如下信息：

    {
        "Authorization": "bearer <your_dashscope_api_key>", // 将<your_dashscope_api_key>替换成您自己的API Key
        "user-agent": "your_platform_info", //可选
        "X-DashScope-WorkSpace": workspace, // 可选
        "X-DashScope-DataInspection": "enable"
    }

**指令（客户端→服务端）**
---------------

指令是客户端发送给服务端的消息，为JSON格式，以Text Frame方式发送，用于控制任务的起止和标识任务边界。

**说明**

客户端发送给服务端的二进制音频（须为单声道音频）不包含在任何指令中，需单独发送。

发送指令需严格遵循以下时序，否则可能导致任务失败：

1.  **发送**[**run-task指令**](#12d8a57443dmz)
    
    *   用于启动语音识别任务。
        
    *   返回的 `task_id` 需在后续发送[finish-task指令](#2e967d2d349es)时使用，必须保持一致。
        
2.  **发送**[**二进制音频**](#e14da5dfe9npt)**（单声道）**
    
    *   用于发送待识别音频。
        
    *   必须在接收到服务端返回的[task-started事件](#2942cede42z9e)后发送音频。
        
3.  **发送**[**finish-task指令**](#2e967d2d349es)
    
    *   用于结束语音识别任务。
        
    *   在音频发送完毕后发送此指令。
        

### **1\. run-task指令：开启任务**

该指令用于开启语音识别任务。`task_id`在后续发送[finish-task指令](#2e967d2d349es)时也需要使用，必须保持一致。

**重要**

**发送时机：**WebSocket连接建立后。

**示例：**

    {
        "header": {
            "action": "run-task",
            "task_id": "2bf83b9a-baeb-4fda-8d9a-xxxxxxxxxxxx", // 随机uuid
            "streaming": "duplex"
        },
        "payload": {
            "task_group": "audio",
            "task": "asr",
            "function": "recognition",
            "model": "paraformer-realtime-v2",
            "parameters": {
                "format": "pcm", // 音频格式
                "sample_rate": 16000, // 采样率
                "vocabulary_id": "vocab-xxx-24ee19fa8cfb4d52902170a0xxxxxxxx", // paraformer-realtime-v2支持的热词ID
                "disfluency_removal_enabled": false, // 过滤语气词
                "language_hints": [
                    "en"
                ] // 指定语言，仅支持paraformer-realtime-v2模型
            },
            "resources": [ //不使用热词功能时，不要传递resources参数
                {
                    "resource_id": "xxxxxxxxxxxx", // paraformer-realtime-v1支持的热词ID
                    "resource_type": "asr_phrase"
                }
            ],
            "input": {}
        }
    }

`**header**`**参数说明：**

**参数**

**类型**

**是否必选**

**说明**

**参数**

**类型**

**是否必选**

**说明**

header.action

string

是

指令类型。

当前指令中，固定为"run-task"。

header.task\_id

string

是

当次任务ID。

为32位通用唯一识别码（UUID），由32个随机生成的字母和数字组成。可以带横线（如 `"2bf83b9a-baeb-4fda-8d9a-xxxxxxxxxxxx"`）或不带横线（如 `"2bf83b9abaeb4fda8d9axxxxxxxxxxxx"`）。大多数编程语言都内置了生成UUID的API，例如Python：

    import uuid
    
    def generateTaskId(self):
        # 生成随机UUID
        return uuid.uuid4().hex

在后续发送[finish-task指令](#2e967d2d349es)时，用到的task\_id需要和发送run-task指令时使用的task\_id保持一致。

header.streaming

string

是

固定字符串："duplex"

`**payload**`**参数说明：**

**参数**

**类型**

**是否必选**

**说明**

**参数**

**类型**

**是否必选**

**说明**

payload.task\_group

string

是

固定字符串："audio"。

payload.task

string

是

固定字符串："asr"。

payload.function

string

是

固定字符串："recognition"。

payload.model

string

是

模型名称，支持的模型请参见[模型列表](#dbdbfe151dv19)。

payload.input

object

是

固定格式：{}。

**payload.parameters**

format

string

是

设置待识别音频格式。

支持的音频格式：pcm、wav、mp3、opus、speex、aac、amr。

**重要**

对于opus和speex格式的音频，需要ogg封装；对于wav格式的音频，需要pcm编码。

sample\_rate

integer

是

设置待识别音频采样率（单位Hz）。

因模型而异：

*   paraformer-realtime-v2支持任意采样率。
    
*   paraformer-realtime-v1仅支持16000Hz采样。
    
*   paraformer-realtime-8k-v2仅支持8000Hz采样率。
    
*   paraformer-realtime-8k-v1仅支持8000Hz采样率。
    

vocabulary\_id

string

否

设置热词ID，若未设置则不生效。v2及更高版本模型设置热词ID时使用该字段。

在本次语音识别中，将应用与该热词ID对应的热词信息。具体使用方法请参见[定制热词](https://help.aliyun.com/zh/model-studio/custom-hot-words)。

disfluency\_removal\_enabled

boolean

否

设置是否过滤语气词：

*   true：过滤语气词
    
*   false（默认）：不过滤语气词
    

language\_hints

array\[string\]

否

设置待识别语言代码。如果无法提前确定语种，可不设置，模型会自动识别语种。

目前支持的语言代码：

*   zh: 中文
    
*   en: 英文
    
*   ja: 日语
    
*   yue: 粤语
    
*   ko: 韩语
    
*   de：德语
    
*   fr：法语
    
*   ru：俄语
    

该参数仅对支持多语言的模型生效（参见[模型列表](#dbdbfe151dv19)）。

semantic\_punctuation\_enabled

boolean

否

设置是否开启语义断句，默认关闭。

*   true：开启语义断句，关闭VAD（Voice Activity Detection，语音活动检测）断句。
    
*   false（默认）：开启VAD（Voice Activity Detection，语音活动检测）断句，关闭语义断句。
    

语义断句准确性更高，适合会议转写场景；VAD（Voice Activity Detection，语音活动检测）断句延迟较低，适合交互场景。

通过调整`semantic_punctuation_enabled`参数，可以灵活切换语音识别的断句方式以适应不同场景需求。

该参数仅在模型为v2及更高版本时生效。

max\_sentence\_silence

integer

否

设置VAD（Voice Activity Detection，语音活动检测）断句的静音时长阈值（单位为ms）。

当一段语音后的静音时长超过该阈值时，系统会判定该句子已结束。

参数范围为200ms至6000ms，默认值为800ms。

该参数仅在`semantic_punctuation_enabled`参数为false（VAD断句）且模型为v2及更高版本时生效。

multi\_threshold\_mode\_enabled

boolean

否

该开关打开时（true）可以防止VAD断句切割过长。默认关闭。

该参数仅在`semantic_punctuation_enabled`参数为false（VAD断句）且模型为v2及更高版本时生效。

punctuation\_prediction\_enabled

boolean

否

设置是否在识别结果中自动添加标点：

*   true（默认）：是
    
*   false：否
    

该参数仅在模型为v2及更高版本时生效。

heartbeat

boolean

否

当需要与服务端保持长连接时，可通过该开关进行控制：

*   true：在持续发送静音音频的情况下，可保持与服务端的连接不中断。
    
*   false（默认）：即使持续发送静音音频，连接也将在60秒后因超时而断开。
    
    静音音频指的是在音频文件或数据流中没有声音信号的内容。静音音频可以通过多种方法生成，例如使用音频编辑软件如Audacity或Adobe Audition，或者通过命令行工具如FFmpeg。
    

该参数仅在模型为v2及更高版本时生效。

inverse\_text\_normalization\_enabled

boolean

否

设置是否开启ITN（Inverse Text Normalization，逆文本正则化）。

默认开启（true）。开启后，中文数字将转换为阿拉伯数字。

该参数仅在模型为v2及更高版本时生效。

**payload.resources（内容为列表，不使用热词功能时，不要传递该参数）**

resource\_id

string

否

热词ID，此次语音识别中生效此热词ID对应的热词信息。默认不启用。需和`resource_type`参数配合使用。

注：`resource_id`对应SDK中的`phrase_id`字段，`phrase_id`为v1版本模型热词方案，不支持v2及后续系列模型。支持该方式热词的模型列表请参考[Paraformer语音识别热词定制与管理](https://help.aliyun.com/zh/model-studio/developer-reference/paraformer-asr-phrase-manager)。

resource\_type

string

否

固定字符串“`asr_phrase`”，需和`resource_id`参数配合使用。

### **2\. finish-task指令：结束任务**

该指令用于结束语音识别任务。音频发送完毕后，客户端可以发送此指令以结束任务。

**重要**

**发送时机：**音频发送完成后。

**示例：**

    {
        "header": {
            "action": "finish-task",
            "task_id": "2bf83b9a-baeb-4fda-8d9a-xxxxxxxxxxxx",
            "streaming": "duplex"
        },
        "payload": {
            "input": {}
        }
    }

`**header**`**参数说明：**

**参数**

**类型**

**是否必选**

**说明**

**参数**

**类型**

**是否必选**

**说明**

header.action

string

是

指令类型。

当前指令中，固定为"finish-task"。

header.task\_id

string

是

当次任务ID。

需要和发送[run-task指令](#12d8a57443dmz)时使用的task\_id保持一致。

header.streaming

string

是

固定字符串："duplex"

`**payload**`**参数说明：**

**参数**

**类型**

**是否必选**

**说明**

**参数**

**类型**

**是否必选**

**说明**

payload.input

object

是

固定格式：{}。

**二进制音频（客户端→服务端）**
------------------

客户端需在收到[task-started事件](#2942cede42z9e)后，再发送待识别的音频流。

可以发送实时音频流（比如从话筒中实时获取到的）或者录音文件音频流，音频应是单声道。

音频通过WebSocket的二进制通道上传。建议每次发送100ms的音频，并间隔100ms。

**事件（服务端→客户端）**
---------------

事件是服务端返回给客户端的消息，为JSON格式，代表不同的处理阶段。

### **1\. task-started事件：任务已开启**

当监听到服务端返回的`task-started`事件时，标志着任务已成功开启。只有在接收到该事件后，才能向服务器发送待识别音频或[finish-task指令](#2e967d2d349es)；否则，任务将执行失败。

`task-started`事件的`payload`没有内容。

**示例：**

    {
        "header": {
            "task_id": "2bf83b9a-baeb-4fda-8d9a-xxxxxxxxxxxx",
            "event": "task-started",
            "attributes": {}
        },
        "payload": {}
    }

`**header**`**参数说明：**

**参数**

**类型**

**说明**

**参数**

**类型**

**说明**

header.event

string

事件类型。

当前事件中，固定为"task-started"。

header.task\_id

string

客户端生成的task\_id

### **2\. result-generated事件：语音识别结果**

客户端发送待识别音频和[finish-task指令](#2e967d2d349es)的同时，服务端持续返回`result-generated`事件，该事件包含语音识别的结果。

可以通过`result-generated`事件中的`payload.sentence.endTime`是否为空来判断该结果是中间结果还是最终结果。

**示例：**

    {
      "header": {
        "task_id": "2bf83b9a-baeb-4fda-8d9a-xxxxxxxxxxxx",
        "event": "result-generated",
        "attributes": {}
      },
      "payload": {
        "output": {
          "sentence": {
            "begin_time": 170,
            "end_time": null,
            "text": "好，我们的一个",
            "words": [
              {
                "begin_time": 170,
                "end_time": 295,
                "text": "好",
                "punctuation": "，"
              },
              {
                "begin_time": 295,
                "end_time": 503,
                "text": "我们",
                "punctuation": ""
              },
              {
                "begin_time": 503,
                "end_time": 711,
                "text": "的一",
                "punctuation": ""
              },
              {
                "begin_time": 711,
                "end_time": 920,
                "text": "个",
                "punctuation": ""
              }
            ]
          }
        },
        "usage": null
      }
    }

`**header**`**参数说明：**

**参数**

**类型**

**说明**

**参数**

**类型**

**说明**

header.event

string

事件类型。

当前事件中，固定为"result-generated"。

header.task\_id

string

客户端生成的task\_id。

`**payload**`**参数说明：**

**参数**

**类型**

**说明**

**参数**

**类型**

**说明**

output

object

output.sentence为识别结果，详细内容见下文。

usage

object

固定为null。

`payload.output.sentence`格式如下：

**参数**

**类型**

**说明**

**参数**

**类型**

**说明**

begin\_time

integer

句子开始时间，单位为ms。

end\_time

integer | null

句子结束时间，如果为中间识别结果则为null，单位为ms。

text

string

识别文本。

words

array

字时间戳信息。

heartbeat

boolean | null

若该值为true，可跳过识别结果的处理。

sentence\_end

boolean

判断给定句子是否已结束。

emo\_tag

string

当前句子的情感：

*   positive：正面情感，如开心、满意
    
*   negative：负面情感，如愤怒、沉闷
    
*   neutral：无明显情感
    

仅paraformer-realtime-8k-v2模型支持情感识别。

情感识别仅在语义断句关闭时生效。语义断句默认为关闭状态，可通过[run-task指令](#12d8a57443dmz)的`semantic_punctuation_enabled`参数控制。

句子情感通过解析result-gnerated事件获取，具体步骤如下：

1.  获取`payload.output.sentence.sentence_end`的值，只有为true时才能进行下一步。
    
2.  通过`payload.output.sentence.emo_tag`字段获取当前句子的情感。
    

emo\_confidence

number

当前句子识别情感的置信度，取值范围：\[0.0,1.0\]。值越大表示置信度越高。

仅paraformer-realtime-8k-v2模型支持情感识别。

情感识别仅在语义断句关闭时生效。语义断句默认为关闭状态，可通过[run-task指令](#12d8a57443dmz)的`semantic_punctuation_enabled`参数控制。

句子情感置信度通过解析result-gnerated事件获取，具体步骤如下：

1.  获取`payload.output.sentence.sentence_end`的值，只有为true时才能进行下一步。
    
2.  通过`payload.output.sentence.emo_confidence`字段获取当前句子的情感置信度。
    

`payload.output.sentence.words`为字时间戳列表，其中每一个word格式如下：

**参数**

**类型**

**说明**

**参数**

**类型**

**说明**

begin\_time

integer

字开始时间，单位为ms。

end\_time

integer

字结束时间，单位为ms。

text

string

字。

punctuation

string

标点。

### **3\. task-finished事件：任务已结束**

当监听到服务端返回的`task-finished`事件时，说明任务已结束。此时可以关闭WebSocket连接并结束程序。

**示例：**

    {
        "header": {
            "task_id": "2bf83b9a-baeb-4fda-8d9a-xxxxxxxxxxxx",
            "event": "task-finished",
            "attributes": {}
        },
        "payload": {
            "output": {},
            "usage": null
        }
    }

`**header**`**参数说明：**

**参数**

**类型**

**说明**

**参数**

**类型**

**说明**

header.event

string

事件类型。

当前事件中，固定为"task-finished"。

header.task\_id

string

客户端生成的task\_id。

### **4\. task-failed事件：任务失败**

如果接收到`task-failed`事件，表示任务失败。此时需要关闭WebSocket连接并处理错误。通过分析报错信息，如果是由于编程问题导致的任务失败，您可以调整代码进行修正。

**示例：**

    {
        "header": {
            "task_id": "2bf83b9a-baeb-4fda-8d9a-xxxxxxxxxxxx",
            "event": "task-failed",
            "error_code": "CLIENT_ERROR",
            "error_message": "request timeout after 23 seconds.",
            "attributes": {}
        },
        "payload": {}
    }

`**header**`**参数说明：**

**参数**

**类型**

**说明**

**参数**

**类型**

**说明**

header.event

string

事件类型。

当前事件中，固定为"task-failed"。

header.task\_id

string

客户端生成的task\_id。

header.error\_code

string

报错类型描述。

header.error\_message

string

具体报错原因。

**关于建连开销和连接复用**
---------------

WebSocket服务支持连接复用以提升资源的利用效率，避免建立连接开销。

服务端收到客户端发送的[run-task指令](#12d8a57443dmz)后，将启动一个新的任务，客户端发送[finish-task指令](#2e967d2d349es)后，服务端在任务完成时返回[task-finished事件](#f11a341732xug)以结束该任务。结束任务后WebSocket连接可以被复用，客户端重新发送[run-task指令](#12d8a57443dmz)即可开启下一个任务。

**重要**

1.  在复用连接中的不同任务需要使用不同 task\_id。
    
2.  如果在任务执行过程中发生失败，服务将依然返回[task-failed事件](#ea4609132a8u7)，并关闭该连接。此时这个连接无法继续复用。
    
3.  如果在任务结束后60秒没有新的任务，连接会超时自动断开。
    

**示例代码**
--------

示例代码仅提供最基础的服务调通实现，实际业务场景的相关代码需您自行开发。

在编写WebSocket客户端代码时，为了同时发送和接收消息，通常采用异步编程。您可以按照以下步骤来编写程序：

1.  **建立WebSocket连接**
    
    调用WebSocket库函数（具体实现方式因编程语言或库函数而异），传入[Headers](#f5b7af17168sz)和[URL](#f8ab6424cbcdk)建立WebSocket连接。
    
2.  **监听服务端消息**
    
    通过 WebSocket 库提供的回调函数（观察者模式），您可以监听服务端返回的消息。具体实现方式因编程语言不同而有所差异。
    
    服务端返回的消息分为两类：二进制音频流和事件。
    
    **监听**[**事件**](#a989eb7099wjv)**：**
    
    *   **task-started：**当接收到[task-started事件](#2942cede42z9e)时，表示任务已成功开启。只有在此事件触发后，才能向服务端发送二进制音频或[finish-task指令](#2e967d2d349es)；否则任务会失败。
        
    *   **result-generated：**客户端发送二进制音频时，服务端可能会持续返回[result-generated事件](#e9420a4d7bock)，该事件包含语音识别结果。
        
    *   **task-finished：**接收到[task-finished事件](#f11a341732xug)时，表示任务已完成。此时可以关闭 WebSocket 连接并结束程序。
        
    *   **task-failed：**如果接收到[task-failed事件](#ea4609132a8u7)，表示任务失败。需要关闭 WebSocket 连接，并根据报错信息调整代码进行修正。
        
    
3.  **向服务端发送消息（请务必注意时序）**
    
    在不同于监听服务端消息的线程（如主线程，具体实现因编程语言而异）中，向服务端发送[指令](#271eb7a50ft6r)和二进制音频。
    
    发送指令需严格遵循以下时序，否则可能导致任务失败：
    
    1.  **发送**[**run-task指令**](#12d8a57443dmz)
        
        *   用于启动语音识别任务。
            
        *   返回的 `task_id` 需在后续发送[finish-task指令](#2e967d2d349es)时使用，必须保持一致。
            
    2.  **发送**[**二进制音频**](#e14da5dfe9npt)**（单声道）**
        
        *   用于发送待识别音频。
            
        *   必须在接收到服务端返回的[task-started事件](#2942cede42z9e)后发送音频。
            
    3.  **发送**[**finish-task指令**](#2e967d2d349es)
        
        *   用于结束语音识别任务。
            
        *   在音频发送完毕后发送此指令。
            
    
4.  **关闭WebSocket连接**
    
    在程序正常结束、运行中出现异常或接收到[task-finished事件](#f11a341732xug)、[task-failed事件](#ea4609132a8u7)时，关闭WebSocket连接。通常通过调用工具库中的`close`函数来实现。
    

点击查看完整示例

如下示例中，使用的音频文件为[asr\_example.wav](https://help-static-aliyun-doc.aliyuncs.com/file-manage-files/zh-CN/20241114/mgiguo/asr_example.wav)。

Go

C#

PHP

Node.js

    package main
    
    import (
    	"encoding/json"
    	"fmt"
    	"io"
    	"log"
    	"net/http"
    	"os"
    	"time"
    
    	"github.com/google/uuid"
    	"github.com/gorilla/websocket"
    )
    
    const (
    	wsURL     = "wss://dashscope.aliyuncs.com/api-ws/v1/inference/" // WebSocket服务器地址
    	audioFile = "asr_example.wav"                                   // 替换为您的音频文件路径
    )
    
    var dialer = websocket.DefaultDialer
    
    func main() {
    	// 若没有将API Key配置到环境变量，可将下行替换为：apiKey := "your_api_key"。不建议在生产环境中直接将API Key硬编码到代码中，以减少API Key泄露风险。
    	apiKey := os.Getenv("DASHSCOPE_API_KEY")
    
    	// 连接WebSocket服务
    	conn, err := connectWebSocket(apiKey)
    	if err != nil {
    		log.Fatal("连接WebSocket失败：", err)
    	}
    	defer closeConnection(conn)
    
    	// 启动一个goroutine来接收结果
    	taskStarted := make(chan bool)
    	taskDone := make(chan bool)
    	startResultReceiver(conn, taskStarted, taskDone)
    
    	// 发送run-task指令
    	taskID, err := sendRunTaskCmd(conn)
    	if err != nil {
    		log.Fatal("发送run-task指令失败：", err)
    	}
    
    	// 等待task-started事件
    	waitForTaskStarted(taskStarted)
    
    	// 发送待识别音频文件流
    	if err := sendAudioData(conn); err != nil {
    		log.Fatal("发送音频失败：", err)
    	}
    
    	// 发送finish-task指令
    	if err := sendFinishTaskCmd(conn, taskID); err != nil {
    		log.Fatal("发送finish-task指令失败：", err)
    	}
    
    	// 等待任务完成或失败
    	<-taskDone
    }
    
    // 定义结构体来表示JSON数据
    type Header struct {
    	Action       string                 `json:"action"`
    	TaskID       string                 `json:"task_id"`
    	Streaming    string                 `json:"streaming"`
    	Event        string                 `json:"event"`
    	ErrorCode    string                 `json:"error_code,omitempty"`
    	ErrorMessage string                 `json:"error_message,omitempty"`
    	Attributes   map[string]interface{} `json:"attributes"`
    }
    
    type Output struct {
    	Sentence struct {
    		BeginTime int64  `json:"begin_time"`
    		EndTime   *int64 `json:"end_time"`
    		Text      string `json:"text"`
    		Words     []struct {
    			BeginTime   int64  `json:"begin_time"`
    			EndTime     *int64 `json:"end_time"`
    			Text        string `json:"text"`
    			Punctuation string `json:"punctuation"`
    		} `json:"words"`
    	} `json:"sentence"`
    	Usage interface{} `json:"usage"`
    }
    
    type Payload struct {
    	TaskGroup  string     `json:"task_group"`
    	Task       string     `json:"task"`
    	Function   string     `json:"function"`
    	Model      string     `json:"model"`
    	Parameters Params     `json:"parameters"`
    	// 不使用热词功能时，不要传递resources参数
    	// Resources  []Resource `json:"resources"`
    	Input      Input      `json:"input"`
    	Output     Output     `json:"output,omitempty"`
    }
    
    type Params struct {
    	Format                   string   `json:"format"`
    	SampleRate               int      `json:"sample_rate"`
    	VocabularyID             string   `json:"vocabulary_id"`
    	DisfluencyRemovalEnabled bool     `json:"disfluency_removal_enabled"`
    	LanguageHints            []string `json:"language_hints"`
    }
    
    // 不使用热词功能时，不要传递resources参数
    type Resource struct {
    	ResourceID   string `json:"resource_id"`
    	ResourceType string `json:"resource_type"`
    }
    
    type Input struct {
    }
    
    type Event struct {
    	Header  Header  `json:"header"`
    	Payload Payload `json:"payload"`
    }
    
    // 连接WebSocket服务
    func connectWebSocket(apiKey string) (*websocket.Conn, error) {
    	header := make(http.Header)
    	header.Add("X-DashScope-DataInspection", "enable")
    	header.Add("Authorization", fmt.Sprintf("bearer %s", apiKey))
    	conn, _, err := dialer.Dial(wsURL, header)
    	return conn, err
    }
    
    // 启动一个goroutine异步接收WebSocket消息
    func startResultReceiver(conn *websocket.Conn, taskStarted chan<- bool, taskDone chan<- bool) {
    	go func() {
    		for {
    			_, message, err := conn.ReadMessage()
    			if err != nil {
    				log.Println("解析服务器消息失败：", err)
    				return
    			}
    			var event Event
    			err = json.Unmarshal(message, &event)
    			if err != nil {
    				log.Println("解析事件失败：", err)
    				continue
    			}
    			if handleEvent(conn, event, taskStarted, taskDone) {
    				return
    			}
    		}
    	}()
    }
    
    // 发送run-task指令
    func sendRunTaskCmd(conn *websocket.Conn) (string, error) {
    	runTaskCmd, taskID, err := generateRunTaskCmd()
    	if err != nil {
    		return "", err
    	}
    	err = conn.WriteMessage(websocket.TextMessage, []byte(runTaskCmd))
    	return taskID, err
    }
    
    // 生成run-task指令
    func generateRunTaskCmd() (string, string, error) {
    	taskID := uuid.New().String()
    	runTaskCmd := Event{
    		Header: Header{
    			Action:    "run-task",
    			TaskID:    taskID,
    			Streaming: "duplex",
    		},
    		Payload: Payload{
    			TaskGroup: "audio",
    			Task:      "asr",
    			Function:  "recognition",
    			Model:     "paraformer-realtime-v2",
    			Parameters: Params{
    				Format:     "wav",
    				SampleRate: 16000,
    			},
    			Input: Input{},
    		},
    	}
    	runTaskCmdJSON, err := json.Marshal(runTaskCmd)
    	return string(runTaskCmdJSON), taskID, err
    }
    
    // 等待task-started事件
    func waitForTaskStarted(taskStarted chan bool) {
    	select {
    	case <-taskStarted:
    		fmt.Println("任务开启成功")
    	case <-time.After(10 * time.Second):
    		log.Fatal("等待task-started超时，任务开启失败")
    	}
    }
    
    // 发送音频数据
    func sendAudioData(conn *websocket.Conn) error {
    	file, err := os.Open(audioFile)
    	if err != nil {
    		return err
    	}
    	defer file.Close()
    
    	buf := make([]byte, 1024) // 假设100ms的音频数据大约为1024字节
    	for {
    		n, err := file.Read(buf)
    		if n == 0 {
    			break
    		}
    		if err != nil && err != io.EOF {
    			return err
    		}
    		err = conn.WriteMessage(websocket.BinaryMessage, buf[:n])
    		if err != nil {
    			return err
    		}
    		time.Sleep(100 * time.Millisecond)
    	}
    	return nil
    }
    
    // 发送finish-task指令
    func sendFinishTaskCmd(conn *websocket.Conn, taskID string) error {
    	finishTaskCmd, err := generateFinishTaskCmd(taskID)
    	if err != nil {
    		return err
    	}
    	err = conn.WriteMessage(websocket.TextMessage, []byte(finishTaskCmd))
    	return err
    }
    
    // 生成finish-task指令
    func generateFinishTaskCmd(taskID string) (string, error) {
    	finishTaskCmd := Event{
    		Header: Header{
    			Action:    "finish-task",
    			TaskID:    taskID,
    			Streaming: "duplex",
    		},
    		Payload: Payload{
    			Input: Input{},
    		},
    	}
    	finishTaskCmdJSON, err := json.Marshal(finishTaskCmd)
    	return string(finishTaskCmdJSON), err
    }
    
    // 处理事件
    func handleEvent(conn *websocket.Conn, event Event, taskStarted chan<- bool, taskDone chan<- bool) bool {
    	switch event.Header.Event {
    	case "task-started":
    		fmt.Println("收到task-started事件")
    		taskStarted <- true
    	case "result-generated":
    		if event.Payload.Output.Sentence.Text != "" {
    			fmt.Println("识别结果：", event.Payload.Output.Sentence.Text)
    		}
    	case "task-finished":
    		fmt.Println("任务完成")
    		taskDone <- true
    		return true
    	case "task-failed":
    		handleTaskFailed(event, conn)
    		taskDone <- true
    		return true
    	default:
    		log.Printf("预料之外的事件：%v", event)
    	}
    	return false
    }
    
    // 处理任务失败事件
    func handleTaskFailed(event Event, conn *websocket.Conn) {
    	if event.Header.ErrorMessage != "" {
    		log.Fatalf("任务失败：%s", event.Header.ErrorMessage)
    	} else {
    		log.Fatal("未知原因导致任务失败")
    	}
    }
    
    // 关闭连接
    func closeConnection(conn *websocket.Conn) {
    	if conn != nil {
    		conn.Close()
    	}
    }
    

示例代码如下：

    using System.Net.WebSockets;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    
    class Program {
        private static ClientWebSocket _webSocket = new ClientWebSocket();
        private static CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private static bool _taskStartedReceived = false;
        private static bool _taskFinishedReceived = false;
        // 若没有将API Key配置到环境变量，可将下行替换为：private const string ApiKey="your_api_key"。不建议在生产环境中直接将API Key硬编码到代码中，以减少API Key泄露风险。
        private static readonly string ApiKey = Environment.GetEnvironmentVariable("DASHSCOPE_API_KEY") ?? throw new InvalidOperationException("DASHSCOPE_API_KEY environment variable is not set.");
    
        // WebSocket服务器地址
        private const string WebSocketUrl = "wss://dashscope.aliyuncs.com/api-ws/v1/inference/";
        // 替换为您的音频文件路径
        private const string AudioFilePath = "asr_example.wav";
    
        static async Task Main(string[] args) {
            // 建立WebSocket连接，配置headers进行鉴权
            _webSocket.Options.SetRequestHeader("Authorization", ApiKey);
            _webSocket.Options.SetRequestHeader("X-DashScope-DataInspection", "enable");
    
            await _webSocket.ConnectAsync(new Uri(WebSocketUrl), _cancellationTokenSource.Token);
    
            // 启动线程异步接收WebSocket消息
            var receiveTask = ReceiveMessagesAsync();
    
            // 发送run-task指令
            string _taskId = Guid.NewGuid().ToString("N"); // 生成32位随机ID
            var runTaskJson = GenerateRunTaskJson(_taskId);
            await SendAsync(runTaskJson);
    
            // 等待task-started事件
            while (!_taskStartedReceived) {
                await Task.Delay(100, _cancellationTokenSource.Token);
            }
    
            // 读取本地文件，向服务器发送待识别音频流
            await SendAudioStreamAsync(AudioFilePath);
    
            // 发送finish-task指令结束任务
            var finishTaskJson = GenerateFinishTaskJson(_taskId);
            await SendAsync(finishTaskJson);
    
            // 等待task-finished事件
            while (!_taskFinishedReceived && !_cancellationTokenSource.IsCancellationRequested) {
                try {
                    await Task.Delay(100, _cancellationTokenSource.Token);
                } catch (OperationCanceledException) {
                    // 任务已被取消，退出循环
                    break;
                }
            }
    
            // 关闭连接
            if (!_cancellationTokenSource.IsCancellationRequested) {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", _cancellationTokenSource.Token);
            }
    
            _cancellationTokenSource.Cancel();
            try {
                await receiveTask;
            } catch (OperationCanceledException) {
                // 忽略操作取消异常
            }
        }
    
        private static async Task ReceiveMessagesAsync() {
            try {
                while (_webSocket.State == WebSocketState.Open && !_cancellationTokenSource.IsCancellationRequested) {
                    var message = await ReceiveMessageAsync(_cancellationTokenSource.Token);
                    if (message != null) {
                        var eventValue = message["header"]?["event"]?.GetValue<string>();
                        switch (eventValue) {
                            case "task-started":
                                Console.WriteLine("任务开启成功");
                                _taskStartedReceived = true;
                                break;
                            case "result-generated":
                                Console.WriteLine($"识别结果：{message["payload"]?["output"]?["sentence"]?["text"]?.GetValue<string>()}");
                                break;
                            case "task-finished":
                                Console.WriteLine("任务完成");
                                _taskFinishedReceived = true;
                                _cancellationTokenSource.Cancel();
                                break;
                            case "task-failed":
                                Console.WriteLine($"任务失败：{message["header"]?["error_message"]?.GetValue<string>()}");
                                _cancellationTokenSource.Cancel();
                                break;
                        }
                    }
                }
            } catch (OperationCanceledException) {
                // 忽略操作取消异常
            }
        }
    
        private static async Task<JsonNode?> ReceiveMessageAsync(CancellationToken cancellationToken) {
            var buffer = new byte[1024 * 4];
            var segment = new ArraySegment<byte>(buffer);
            var result = await _webSocket.ReceiveAsync(segment, cancellationToken);
    
            if (result.MessageType == WebSocketMessageType.Close) {
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", cancellationToken);
                return null;
            }
    
            var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
            return JsonNode.Parse(message);
        }
    
        private static async Task SendAsync(string message) {
            var buffer = Encoding.UTF8.GetBytes(message);
            var segment = new ArraySegment<byte>(buffer);
            await _webSocket.SendAsync(segment, WebSocketMessageType.Text, true, _cancellationTokenSource.Token);
        }
    
        private static async Task SendAudioStreamAsync(string filePath) {
            using (var audioStream = File.OpenRead(filePath)) {
                var buffer = new byte[1024]; // 每次发送100ms的音频数据
                int bytesRead;
    
                while ((bytesRead = await audioStream.ReadAsync(buffer, 0, buffer.Length)) > 0) {
                    var segment = new ArraySegment<byte>(buffer, 0, bytesRead);
                    await _webSocket.SendAsync(segment, WebSocketMessageType.Binary, true, _cancellationTokenSource.Token);
                    await Task.Delay(100); // 间隔100ms
                }
            }
        }
    
        private static string GenerateRunTaskJson(string taskId) {
            var runTask = new JsonObject {
                ["header"] = new JsonObject {
                    ["action"] = "run-task",
                    ["task_id"] = taskId,
                    ["streaming"] = "duplex"
                },
                ["payload"] = new JsonObject {
                    ["task_group"] = "audio",
                    ["task"] = "asr",
                    ["function"] = "recognition",
                    ["model"] = "paraformer-realtime-v2",
                    ["parameters"] = new JsonObject {
                        ["format"] = "wav",
                        ["sample_rate"] = 16000,
                        ["vocabulary_id"] = "vocab-xxx-24ee19fa8cfb4d52902170a0xxxxxxxx",
                        ["disfluency_removal_enabled"] = false
                    },
                    // 不使用热词功能时，不要传递resources参数
                    //["resources"] = new JsonArray {
                    //    new JsonObject {
                    //        ["resource_id"] = "xxxxxxxxxxxx",
                    //        ["resource_type"] = "asr_phrase"
                    //    }
                    //},
                    ["input"] = new JsonObject()
                }
            };
            return JsonSerializer.Serialize(runTask);
        }
    
        private static string GenerateFinishTaskJson(string taskId) {
            var finishTask = new JsonObject {
                ["header"] = new JsonObject {
                    ["action"] = "finish-task",
                    ["task_id"] = taskId,
                    ["streaming"] = "duplex"
                },
                ["payload"] = new JsonObject {
                    ["input"] = new JsonObject()
                }
            };
            return JsonSerializer.Serialize(finishTask);
        }
    }

示例代码目录结构为：

my-php-project/

├── composer.json

├── vendor/

└── index.php

composer.json内容如下，相关依赖的版本号请根据实际情况自行决定：

    {
        "require": {
            "react/event-loop": "^1.3",
            "react/socket": "^1.11",
            "react/stream": "^1.2",
            "react/http": "^1.1",
            "ratchet/pawl": "^0.4"
        },
        "autoload": {
            "psr-4": {
                "App\\": "src/"
            }
        }
    }

index.php内容如下：

    <?php
    
    require __DIR__ . '/vendor/autoload.php';
    
    use Ratchet\Client\Connector;
    use React\EventLoop\Loop;
    use React\Socket\Connector as SocketConnector;
    use Ratchet\rfc6455\Messaging\Frame;
    
    # 若没有将API Key配置到环境变量，可将下行替换为：$api_key="your_api_key"。不建议在生产环境中直接将API Key硬编码到代码中，以减少API Key泄露风险。
    $api_key = getenv("DASHSCOPE_API_KEY");
    $websocket_url = 'wss://dashscope.aliyuncs.com/api-ws/v1/inference/'; // WebSocket服务器地址
    $audio_file_path = 'asr_example.wav'; // 替换为您的音频文件路径
    
    $loop = Loop::get();
    
    // 创建自定义的连接器
    $socketConnector = new SocketConnector($loop, [
        'tcp' => [
            'bindto' => '0.0.0.0:0',
        ],
        'tls' => [
            'verify_peer' => false,
            'verify_peer_name' => false,
        ],
    ]);
    
    $connector = new Connector($loop, $socketConnector);
    
    $headers = [
        'Authorization' => 'bearer ' . $api_key,
        'X-DashScope-DataInspection' => 'enable'
    ];
    
    $connector($websocket_url, [], $headers)->then(function ($conn) use ($loop, $audio_file_path) {
        echo "连接到WebSocket服务器\n";
    
        // 启动异步接收WebSocket消息的线程
        $conn->on('message', function($msg) use ($conn, $loop, $audio_file_path) {
            $response = json_decode($msg, true);
    
            if (isset($response['header']['event'])) {
                handleEvent($conn, $response, $loop, $audio_file_path);
            } else {
                echo "未知的消息格式\n";
            }
        });
    
        // 监听连接关闭
        $conn->on('close', function($code = null, $reason = null) {
            echo "连接已关闭\n";
            if ($code !== null) {
                echo "关闭代码: " . $code . "\n";
            }
            if ($reason !== null) {
                echo "关闭原因：" . $reason . "\n";
            }
        });
    
        // 生成任务ID
        $taskId = generateTaskId();
    
        // 发送 run-task 指令
        sendRunTaskMessage($conn, $taskId);
    
    }, function ($e) {
        echo "无法连接：{$e->getMessage()}\n";
    });
    
    $loop->run();
    
    /**
     * 生成任务ID
     * @return string
     */
    function generateTaskId(): string {
        return bin2hex(random_bytes(16));
    }
    
    /**
     * 发送 run-task 指令
     * @param $conn
     * @param $taskId
     */
    function sendRunTaskMessage($conn, $taskId) {
        $runTaskMessage = json_encode([
            "header" => [
                "action" => "run-task",
                "task_id" => $taskId,
                "streaming" => "duplex"
            ],
            "payload" => [
                "task_group" => "audio",
                "task" => "asr",
                "function" => "recognition",
                "model" => "paraformer-realtime-v2",
                "parameters" => [
                    "format" => "wav",
                    "sample_rate" => 16000
                ],
                // 不使用热词功能时，不要传递resources参数
                //"resources" => [
                //    [
                //        "resource_id" => "xxxxxxxxxxxx",
                //        "resource_type" => "asr_phrase"
                //    ]
                //],
                "input" => []
            ]
        ]);
        echo "准备发送run-task指令：" . $runTaskMessage . "\n";
        $conn->send($runTaskMessage);
        echo "run-task指令已发送\n";
    }
    
    /**
     * 读取音频文件
     * @param string $filePath
     * @return bool|string
     */
    function readAudioFile(string $filePath) {
        $voiceData = file_get_contents($filePath);
        if ($voiceData === false) {
            echo "无法读取音频文件\n";
        }
        return $voiceData;
    }
    
    /**
     * 分割音频数据
     * @param string $data
     * @param int $chunkSize
     * @return array
     */
    function splitAudioData(string $data, int $chunkSize): array {
        return str_split($data, $chunkSize);
    }
    
    /**
     * 发送 finish-task 指令
     * @param $conn
     * @param $taskId
     */
    function sendFinishTaskMessage($conn, $taskId) {
        $finishTaskMessage = json_encode([
            "header" => [
                "action" => "finish-task",
                "task_id" => $taskId,
                "streaming" => "duplex"
            ],
            "payload" => [
                "input" => []
            ]
        ]);
        echo "准备发送finish-task指令: " . $finishTaskMessage . "\n";
        $conn->send($finishTaskMessage);
        echo "finish-task指令已发送\n";
    }
    
    /**
     * 处理事件
     * @param $conn
     * @param $response
     * @param $loop
     * @param $audio_file_path
     */
    function handleEvent($conn, $response, $loop, $audio_file_path) {
        static $taskId;
        static $chunks;
        static $allChunksSent = false;
    
        if (is_null($taskId)) {
            $taskId = generateTaskId();
        }
    
        switch ($response['header']['event']) {
            case 'task-started':
                echo "任务开始，发送音频数据...\n";
                // 读取音频文件
                $voiceData = readAudioFile($audio_file_path);
                if ($voiceData === false) {
                    echo "无法读取音频文件\n";
                    $conn->close();
                    return;
                }
    
                // 分割音频数据
                $chunks = splitAudioData($voiceData, 1024);
    
                // 定义发送函数
                $sendChunk = function() use ($conn, &$chunks, $loop, &$sendChunk, &$allChunksSent, $taskId) {
                    if (!empty($chunks)) {
                        $chunk = array_shift($chunks);
                        $binaryMsg = new Frame($chunk, true, Frame::OP_BINARY);
                        $conn->send($binaryMsg);
                        // 100ms后发送下一个片段
                        $loop->addTimer(0.1, $sendChunk);
                    } else {
                        echo "所有数据块已发送\n";
                        $allChunksSent = true;
    
                        // 发送 finish-task 指令
                        sendFinishTaskMessage($conn, $taskId);
                    }
                };
    
                // 开始发送音频数据
                $sendChunk();
                break;
            case 'result-generated':
                $result = $response['payload']['output']['sentence'];
                echo "识别结果：" . $result['text'] . "\n";
                break;
            case 'task-finished':
                echo "任务完成\n";
                $conn->close();
                break;
            case 'task-failed':
                echo "任务失败\n";
                echo "错误代码：" . $response['header']['error_code'] . "\n";
                echo "错误信息：" . $response['header']['error_message'] . "\n";
                $conn->close();
                break;
            case 'error':
                echo "错误：" . $response['payload']['message'] . "\n";
                break;
            default:
                echo "未知事件：" . $response['header']['event'] . "\n";
                break;
        }
    
        // 如果所有数据已发送且任务已完成，关闭连接
        if ($allChunksSent && $response['header']['event'] == 'task-finished') {
            // 等待1秒以确保所有数据都已传输完毕
            $loop->addTimer(1, function() use ($conn) {
                $conn->close();
                echo "客户端关闭连接\n";
            });
        }
    }

需安装相关依赖：

    npm install ws
    npm install uuid

示例代码如下：

    const fs = require('fs');
    const WebSocket = require('ws');
    const { v4: uuidv4 } = require('uuid'); // 用于生成UUID
    
    // 若没有将API Key配置到环境变量，可将下行替换为：apiKey = 'your_api_key'。不建议在生产环境中直接将API Key硬编码到代码中，以减少API Key泄露风险。
    const apiKey = process.env.DASHSCOPE_API_KEY;
    const url = 'wss://dashscope.aliyuncs.com/api-ws/v1/inference/'; // WebSocket服务器地址
    const audioFile = 'asr_example.wav'; // 替换为您的音频文件路径
    
    // 生成32位随机ID
    const TASK_ID = uuidv4().replace(/-/g, '').slice(0, 32);
    
    // 创建WebSocket客户端
    const ws = new WebSocket(url, {
      headers: {
        Authorization: `bearer ${apiKey}`,
        'X-DashScope-DataInspection': 'enable'
      }
    });
    
    let taskStarted = false; // 标记任务是否已启动
    
    // 连接打开时发送run-task指令
    ws.on('open', () => {
      console.log('连接到服务器');
      sendRunTask();
    });
    
    // 接收消息处理
    ws.on('message', (data) => {
      const message = JSON.parse(data);
      switch (message.header.event) {
        case 'task-started':
          console.log('任务开始');
          taskStarted = true;
          sendAudioStream();
          break;
        case 'result-generated':
          console.log('识别结果：', message.payload.output.sentence.text);
          break;
        case 'task-finished':
          console.log('任务完成');
          ws.close();
          break;
        case 'task-failed':
          console.error('任务失败：', message.header.error_message);
          ws.close();
          break;
        default:
          console.log('未知事件：', message.header.event);
      }
    });
    
    // 如果没有收到task-started事件，关闭连接
    ws.on('close', () => {
      if (!taskStarted) {
        console.error('任务未启动，关闭连接');
      }
    });
    
    // 发送run-task指令
    function sendRunTask() {
      const runTaskMessage = {
        header: {
          action: 'run-task',
          task_id: TASK_ID,
          streaming: 'duplex'
        },
        payload: {
          task_group: 'audio',
          task: 'asr',
          function: 'recognition',
          model: 'paraformer-realtime-v2',
          parameters: {
            sample_rate: 16000,
            format: 'wav'
          },
          input: {}
        }
      };
      ws.send(JSON.stringify(runTaskMessage));
    }
    
    // 发送音频流
    function sendAudioStream() {
      const audioStream = fs.createReadStream(audioFile);
      let chunkCount = 0;
    
      function sendNextChunk() {
        const chunk = audioStream.read();
        if (chunk) {
          ws.send(chunk);
          chunkCount++;
          setTimeout(sendNextChunk, 100); // 每100ms发送一次
        }
      }
    
      audioStream.on('readable', () => {
        sendNextChunk();
      });
    
      audioStream.on('end', () => {
        console.log('音频流结束');
        sendFinishTask();
      });
    
      audioStream.on('error', (err) => {
        console.error('读取音频文件错误：', err);
        ws.close();
      });
    }
    
    // 发送finish-task指令
    function sendFinishTask() {
      const finishTaskMessage = {
        header: {
          action: 'finish-task',
          task_id: TASK_ID,
          streaming: 'duplex'
        },
        payload: {
          input: {}
        }
      };
      ws.send(JSON.stringify(finishTaskMessage));
    }
    
    // 错误处理
    ws.on('error', (error) => {
      console.error('WebSocket错误：', error);
    });

**错误码**
-------

在使用API过程中，如果调用失败并返回错误信息，请参见[错误信息](https://help.aliyun.com/zh/model-studio/error-code)进行解决。

**常见问题**
--------

### **功能特性**

#### **Q：在长时间静默的情况下，如何保持与服务端长连接？**

将请求参数`heartbeat`设置为true，并持续向服务端发送静音音频。

静音音频指的是在音频文件或数据流中没有声音信号的内容。静音音频可以通过多种方法生成，例如使用音频编辑软件如Audacity或Adobe Audition，或者通过命令行工具如FFmpeg。

#### **Q：如何识别本地文件（录音文件）？**

将本地文件转成二进制音频流，通过WebSocket的二进制通道上传二进制音频流进行识别（通常为WebSocket库的send方法）。代码片段如下所示，完整示例请参见[示例代码](#f2e763e39dw6i)。

点击查看代码片段

Go

C#

PHP

Node.js

Go

    // 发送音频数据
    func sendAudioData(conn *websocket.Conn) error {
    	file, err := os.Open(audioFile)
    	if err != nil {
    		return err
    	}
    	defer file.Close()
    
    	buf := make([]byte, 1024) // 假设100ms的音频数据大约为1024字节
    	for {
    		n, err := file.Read(buf)
    		if n == 0 {
    			break
    		}
    		if err != nil && err != io.EOF {
    			return err
    		}
    		err = conn.WriteMessage(websocket.BinaryMessage, buf[:n])
    		if err != nil {
    			return err
    		}
    		time.Sleep(100 * time.Millisecond)
    	}
    	return nil
    }

C#

    private static async Task SendAudioStreamAsync(string filePath) {
        using (var audioStream = File.OpenRead(filePath)) {
            var buffer = new byte[1024]; // 每次发送100ms的音频数据
            int bytesRead;
    
            while ((bytesRead = await audioStream.ReadAsync(buffer, 0, buffer.Length)) > 0) {
                var segment = new ArraySegment<byte>(buffer, 0, bytesRead);
                await _webSocket.SendAsync(segment, WebSocketMessageType.Binary, true, _cancellationTokenSource.Token);
                await Task.Delay(100); // 间隔100ms
            }
        }
    }

PHP

    // 读取音频文件
    $voiceData = readAudioFile($audio_file_path);
    if ($voiceData === false) {
        echo "无法读取音频文件\n";
        $conn->close();
        return;
    }
    
    // 分割音频数据
    $chunks = splitAudioData($voiceData, 1024);
    
    // 定义发送函数
    $sendChunk = function() use ($conn, &$chunks, $loop, &$sendChunk, &$allChunksSent, $taskId) {
        if (!empty($chunks)) {
            $chunk = array_shift($chunks);
            $binaryMsg = new Frame($chunk, true, Frame::OP_BINARY);
            $conn->send($binaryMsg);
            // 100ms后发送下一个片段
            $loop->addTimer(0.1, $sendChunk);
        } else {
            echo "所有数据块已发送\n";
            $allChunksSent = true;
    
            // 发送 finish-task 指令
            sendFinishTaskMessage($conn, $taskId);
        }
    };
    
    // 开始发送音频数据
    $sendChunk();

Node.js

    // 发送音频流
    function sendAudioStream() {
      const audioStream = fs.createReadStream(audioFile);
      let chunkCount = 0;
    
      function sendNextChunk() {
        const chunk = audioStream.read();
        if (chunk) {
          ws.send(chunk);
          chunkCount++;
          setTimeout(sendNextChunk, 100); // 每100ms发送一次
        }
      }
    
      audioStream.on('readable', () => {
        sendNextChunk();
      });
    
      audioStream.on('end', () => {
        console.log('音频流结束');
        sendFinishTask();
      });
    
      audioStream.on('error', (err) => {
        console.error('读取音频文件错误：', err);
        ws.close();
      });
    }

.unionContainer .markdown-body p + blockquote { margin-top: 4px; } /\* 让表格里的引用上下间距调为 0 ，避免换行时不连续 \*/ .unionContainer .markdown-body blockquote { margin: 0; } .unionContainer .markdown-body table { table-layout: auto; } .unionContainer .markdown-body table tr { border-bottom: 1px solid #E9E9E9; } .unionContainer .markdown-body table.table-no-border tr { border: none; } .unionContainer .markdown-body table tr:last-child { border: none; } .unionContainer .markdown-body h2 span.ph.tips { color: #666; font-size: 16px; }