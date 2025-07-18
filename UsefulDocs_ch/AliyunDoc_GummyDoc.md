Page Title

本文介绍如何通过WebSocket连接访问Gummy实时语音识别、翻译服务。

DashScope SDK目前仅支持Java和Python。若想使用其他编程语言开发Gummy实时语音识别、翻译应用程序，可以通过WebSocket连接与服务进行通信。

WebSocket是一种支持全双工通信的网络协议。客户端和服务器通过一次握手建立持久连接，双方可以互相主动推送数据，因此在实时性和效率方面具有显著优势。

对于常用编程语言，有许多现成的WebSocket库和示例可供参考，例如：

*   Go：`gorilla/websocket`
    
*   PHP：`Ratchet`
    
*   Node.js：`ws`
    

建议您先了解WebSocket的基本原理和技术细节，再参照本文进行开发。

**模型列表**
--------

**模型名**

**模型简介**

gummy-realtime-v1

Gummy实时语音识别、翻译模型。默认进行标点符号预测和逆文本正则化（INT，Inverse Text Normalization）。支持[定制热词](https://help.aliyun.com/zh/model-studio/custom-hot-words-for-gummy)。

模型使用VAD（Voice Activity Detection）断句。

**前提条件**
--------

已开通服务并获得API-KEY：[获取API Key](https://help.aliyun.com/zh/model-studio/get-api-key)。建议您[配置API Key到环境变量](https://help.aliyun.com/zh/model-studio/configure-api-key-through-environment-variables)，从而避免在代码里显示配置API Key，降低泄漏风险。

**约束**
------

**接口调用方式限制**：不支持前端直接调用API，需通过后端中转。

**客户端与服务端的交互流程**
----------------

按时间顺序，客户端与服务端的交互流程如下：

1.  建立连接：客户端与服务端建立WebSocket连接。
    
2.  开启任务：
    
    *   客户端发送`run-task`指令以开启任务。
        
    *   客户端收到服务端返回的`task-started`事件，标志着任务已成功开启，可以进行后续步骤。
        
3.  发送音频流：
    
    *   客户端开始发送音频流，并同时接收服务端持续返回的`result-generated`事件，该事件包含语音识别结果。
        
4.  通知服务端结束任务：
    
    *   客户端发送`finish-task`指令通知服务端结束任务，并继续接收服务端返回的`result-generated`事件。
        
5.  任务结束：
    
    *   客户端收到服务端返回的`task-finished`事件，标志着任务结束。
        
6.  关闭连接：客户端关闭WebSocket连接。
    

![image](https://help-static-aliyun-doc.aliyuncs.com/assets/img/zh-CN/6602721571/CAEQURiBgMCczta5pxkiIGY0N2Q2YjIwZTM1MTQyNTY4ZmFkY2MwN2JmOTllODFl4709861_20241015153444.149.svg)

WebSocket客户端编程与消息处理
-------------------

在编写WebSocket客户端代码时，为了同时发送和接收消息，通常采用异步编程。您可以按照以下步骤来编写程序：

1.  建立WebSocket连接：首先，初始化并建立与服务器的WebSocket连接。
    
2.  异步监听服务器消息：启动一个单独的线程（具体实现方式因编程语言而异）来监听服务器返回的消息，根据消息内容进行相应的操作。
    
3.  发送消息：在不同于监听服务器消息的线程中（例如主线程，具体实现方式因编程语言而异），向服务器发送消息。
    
4.  关闭连接：在程序结束前，确保关闭WebSocket连接以释放资源。
    

当然，编程思路不止这一种，您或许有更好的想法。本文主要介绍通过WebSocket连接访问服务时的鉴权细节及客户端与服务端之间的消息交互。由于篇幅有限，其他思路将不再赘述。

接下来将按照上述思路，为您详细说明。

### **一、建立WebSocket连接**

调用WebSocket库函数（具体实现方式因编程语言或库函数而异），将请求头和URL传入以建立WebSocket连接。

请求头中需添加如下鉴权信息：

    {
        "Authorization": "bearer <your_dashscope_api_key>", // 将<your_dashscope_api_key>替换成您自己的API Key
        "user-agent": "your_platform_info", //可选
        "X-DashScope-WorkSpace": workspace, // 可选
        "X-DashScope-DataInspection": "enable"
    }

WebSocket URL固定如下：

    wss://dashscope.aliyuncs.com/api-ws/v1/inference

### **二、异步监听服务器返回的消息**

如上所述，您可以启动一个线程（具体实现因编程语言而异）来监听服务器返回的消息。WebSocket库通常会提供回调函数（观察者模式）来处理这些消息。您可以在回调函数中根据不同的消息类型实现相应的功能。

服务端返回给客户端的消息叫做事件，事件代表不同的处理阶段，为JSON格式，由`header`和`payload`这两部分组成：

*   `header`：包含基础信息，格式较为统一。
    
    除`task-failed`外，所有事件的`header`格式统一。
    
    `header`示例：
    
        {
            "header": {
                "task_id": "2bf83b9a-baeb-4fda-8d9a-xxxxxxxxxxxx",
                "event": "task-started",
                "attributes": {}
            }
        }
    
    `header`参数：
    
    **参数**
    
    **类型**
    
    **说明**
    
    header
    
    object
    
    请求头
    
    header.event
    
    string
    
    事件类型
    
    *   task-started
        
    *   result-generated
        
    *   task-finished
        
    *   task-failed
        
    
    详细说明参见下文。
    
    header.task\_id
    
    string
    
    客户端生成的task\_id
    
*   `payload`：包含基础信息外的其他信息。不同事件的`payload`格式可能不同。
    

共有如下四种事件：

##### 1、task-started事件：语音识别任务已开启

当监听到服务端返回的`task-started`事件时，标志着任务已成功开启。只有在接收到该事件后，才能向服务器发送待识别音频或`finish-task`指令；否则，任务将执行失败。

`task-started`事件的`payload`没有内容。

示例：

    {
        "header": {
            "task_id": "2bf83b9a-baeb-4fda-8d9a-xxxxxxxxxxxx",
            "event": "task-started",
            "attributes": {}
        },
        "payload": {}
    }

##### 2、result-generated事件：包含语音识别响应结果

客户端发送待识别音频和`finish-task`指令的同时，服务端持续返回`result-generated`事件，该事件包含语音识别的结果。

可以通过`result-generated`事件中的`sentence_end`是否为True来判断该结果是中间结果还是最终结果。

示例：

    {
    	"header": {
    		"task_id": "2bf83b9a-baeb-4fda-8d9a-xxxxxxxxxxxx",
    		"event": "result-generated",
    		"attributes": {}
    	},
    	"payload": {
    		"output": {
    			"translations": [{
    				"sentence_id": 0,
    				"begin_time": 100,
    				"end_time": 2720,
    				"text": "This is a text used for testing.",
    				"lang": "en",
    				"words": [{
    						"begin_time": 100,
    						"end_time": 427,
    						"text": "This",
    						"punctuation": "This",
    						"fixed": true,
    						"speaker_id": null
    					},
    					{
    						"begin_time": 427,
    						"end_time": 755,
    						"text": " is",
    						"punctuation": " is",
    						"fixed": true,
    						"speaker_id": null
    					},
    					{
    						"begin_time": 755,
    						"end_time": 1082,
    						"text": " a",
    						"punctuation": " a",
    						"fixed": true,
    						"speaker_id": null
    					},
    					{
    						"begin_time": 1082,
    						"end_time": 1410,
    						"text": " text",
    						"punctuation": " text",
    						"fixed": true,
    						"speaker_id": null
    					},
    					{
    						"begin_time": 1410,
    						"end_time": 1737,
    						"text": " used",
    						"punctuation": " used",
    						"fixed": true,
    						"speaker_id": null
    					},
    					{
    						"begin_time": 1737,
    						"end_time": 2065,
    						"text": " for",
    						"punctuation": " for",
    						"fixed": true,
    						"speaker_id": null
    					},
    					{
    						"begin_time": 2065,
    						"end_time": 2392,
    						"text": " testing",
    						"punctuation": " testing",
    						"fixed": true,
    						"speaker_id": null
    					},
    					{
    						"begin_time": 2392,
    						"end_time": 2720,
    						"text": ".",
    						"punctuation": ".",
    						"fixed": true,
    						"speaker_id": null
    					}
    				],
    				"sentence_end": true
    			}],
    			"transcription": {
    				"sentence_id": 0,
    				"begin_time": 100,
    				"end_time": 2720,
    				"text": "这是一句用来测试的文本。",
    				"words": [{
    						"begin_time": 100,
    						"end_time": 427,
    						"text": "这",
    						"punctuation": "这",
    						"fixed": true,
    						"speaker_id": null
    					},
    					{
    						"begin_time": 427,
    						"end_time": 755,
    						"text": "是一",
    						"punctuation": "是一",
    						"fixed": true,
    						"speaker_id": null
    					},
    					{
    						"begin_time": 755,
    						"end_time": 1082,
    						"text": "句",
    						"punctuation": "句",
    						"fixed": true,
    						"speaker_id": null
    					},
    					{
    						"begin_time": 1082,
    						"end_time": 1410,
    						"text": "用来",
    						"punctuation": "用来",
    						"fixed": true,
    						"speaker_id": null
    					},
    					{
    						"begin_time": 1410,
    						"end_time": 1737,
    						"text": "测试",
    						"punctuation": "测试",
    						"fixed": true,
    						"speaker_id": null
    					},
    					{
    						"begin_time": 1737,
    						"end_time": 2065,
    						"text": "的",
    						"punctuation": "的",
    						"fixed": true,
    						"speaker_id": null
    					},
    					{
    						"begin_time": 2065,
    						"end_time": 2392,
    						"text": "文本",
    						"punctuation": "文本",
    						"fixed": true,
    						"speaker_id": null
    					},
    					{
    						"begin_time": 2392,
    						"end_time": 2720,
    						"text": "。",
    						"punctuation": "。",
    						"fixed": true,
    						"speaker_id": null
    					}
    				],
    				"sentence_end": true
    			}
    		}
    	}
    }

**重要**

当sentence\_end=false时，为中间结果，在中间结果中，不保证识别和翻译进度同步，需要等待一句话结束（sentence\_end=true）时同步。

`payload`参数说明：

**参数**

**类型**

**说明**

output

object

output.translations为翻译结果，output.transcription为识别结果，详细内容见下文。

`payload.output.transcription`格式如下：

**参数**

**类型**

**说明**

sentence\_id

integer

句子ID。

begin\_time

integer

句子开始时间，单位为ms。

end\_time

integer

句子结束时间，单位为ms。

text

string

识别文本。

words

array\[Word\]

字时间戳信息。

stash

object

语义断句发生时，后一句未断句的识别结果。

sentence\_end

boolean

当前文本是否构成完整的句子。

*   true：当前文本构成完整句子，识别结果为最终结果。
    
*   false：当前文本未构成完整句子，识别结果可能会更新。
    

`payload.output.translations`的值为数组，代表不同翻译目标语言对应的结果。数组元素格式如下：

**参数**

**类型**

**说明**

sentence\_id

integer

句子ID。

lang

string

翻译语种。

begin\_time

integer

句子开始时间，单位为ms。

end\_time

integer

句子结束时间，单位为ms。

text

string

识别文本。

words

array\[Word\]

字时间戳信息。

stash

object

语义断句发生时，后一句未断句的识别结果。

sentence\_end

boolean

当前文本是否构成完整的句子。

*   true：当前文本构成完整句子，翻译结果为最终结果。
    
*   false：当前文本未构成完整句子，翻译结果可能会更新。
    

`transcription`或`translations`中的`word`为字时间戳列表，其中每一个word格式如下：

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

fixed

boolean

中间结果`text`的内容是否可能发生变化。

`transcription`或`translations`中的`stash`代表后一句未断句识别结果：

如果当前识别或翻译中，部分文本成句，则成句部分会在`payload.output.transcription.text`或`payload.output.translations[index].text`中，剩余未成句的结果会出现在`Stash`中。成句结果和`Stash`共同组成当前的完整结果。

以下示例展示了一次实时识别中`TranscriptionResult`和`Stash`的变化：

**No**

`**payload.output.transcription.text**`

`**payload.output.transcription.text**`

1

早

\-

2

早上

\-

3

早上好

\-

4

早上好，今天

\-

5

早上好。

今天的

6

今天的天气

\-

7

今天的天气怎么样？

\-

##### 3、task-finished事件：语音识别任务已结束

当监听到服务端返回的`task-finished`事件时，说明任务已结束。此时可以关闭WebSocket连接并结束程序。

示例：

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

##### 4、task-failed事件：任务失败

如果接收到`task-failed`事件，表示任务失败。此时需要关闭WebSocket连接并处理错误。通过分析报错信息，如果是由于编程问题导致的任务失败，您可以调整代码进行修正。

示例：

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

`header`参数说明：

**参数**

**类型**

**说明**

header.error\_code

string

报错类型描述。

header.error\_message

string

具体报错原因。

### 三、给服务器发送消息

在与监听服务器消息不同的线程中（比如主线程，具体实现因编程语言而异），向服务器发送消息。

客户端发送给服务端的消息有两种：

1.  音频流（须为单声道音频）。
    
2.  指令：以Text Frame方式发送的JSON格式的数据，用于控制任务的起止和标识任务边界。
    
    指令由`header`和`payload`这两部分组成：
    
    *   header：包含基础信息，格式统一。
        
        `header`示例：
        
            {
                "header": {
                    "action": "run-task",
                    "task_id": "2bf83b9a-baeb-4fda-8d9a-xxxxxxxxxxxx", // 随机uuid
                    "streaming": "duplex"
                }
            }
        
        `header`参数：
        
        **参数**
        
        **类型**
        
        **是否必选**
        
        **说明**
        
        header
        
        object
        
        \-
        
        请求头
        
        header.action
        
        string
        
        是
        
        指令类型，可以选填
        
        *   "run-task"
            
        *   "finish-task"
            
        
        用法参见下文。
        
        header.task\_id
        
        string
        
        是
        
        当次任务ID，随机生成的32位唯一ID。
        
        为32位通用唯一识别码（UUID），由32个随机生成的字母和数字组成。可以带横线（如 `"2bf83b9a-baeb-4fda-8d9a-xxxxxxxxxxxx"`）或不带横线（如 `"2bf83b9abaeb4fda8d9axxxxxxxxxxxx"`）。大多数编程语言都内置了生成UUID的API，例如Python：
        
            import uuid
            
            def generateTaskId(self):
                # 生成随机UUID
                return uuid.uuid4().hex
        
        header.streaming
        
        string
        
        是
        
        固定字符串："duplex"
        
    *   `payload`：包含基础信息外的其他信息。不同指令的`payload`格式可能不同。
        
    

向服务器发送消息需要遵循如下时序，否则会导致任务失败：首先发送`run-task`指令，待监听到服务器返回的`task-started`事件后，再发送待识别的音频流。在音频流发送结束后，发送`finish-task`指令。

##### 1、发送run-task指令：开启语音识别任务（支持定制热词）

该指令用于开启语音识别、翻译任务。`task_id`在后续发送`finish-task`指令时也需要使用，必须保持一致。

示例：

    {
    	"header": {
    		"streaming": "duplex",
    		"task_id": "e34730287cf643a6b0f1c7114c3ee899",
    		"action": "run-task"
    	},
    	"payload": {
    		"model": "gummy-realtime-v1",
    		"parameters": {
    			"sample_rate": 16000,
    			"format": "pcm",
    			"source_language": null,
    			"transcription_enabled": true,
    			"translation_enabled": true,
    			"translation_target_languages": ["en"]
    		},
    		"input": {},
    		"task": "asr",
    		"task_group": "audio",
    		"function": "recognition"
    	}
    }

`payload`参数说明：

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

模型名称，详情请参见[模型列表](#4430e95874ceh)。

payload.input

object

是

固定格式：{}。

payload.parameters支持参数：

**参数**

**类型**

**默认值**

**是否必须**

**说明**

**参数**

**类型**

**默认值**

**是否必须**

**说明**

sample\_rate

integer

\-

是

设置待识别音频采样率（单位Hz）。支持16000Hz及以上采样率。

format

string

\-

是

设置待识别音频格式。

支持的音频格式：pcm、wav、mp3、opus、speex、aac、amr。

**重要**

对于opus和speex格式的音频，需要ogg封装；对于wav格式的音频，需要pcm编码。

vocabulary\_id

string

\-

否

设置热词ID，若未设置则不生效。

在本次语音识别中，将应用与该热词ID对应的热词信息。具体使用方法请参见[定制热词](https://help.aliyun.com/zh/model-studio/custom-hot-words-for-gummy)。

source\_language

string

auto

否

设置源语言（待识别/翻译语言）代码。如果无法提前确定语种，可不设置，默认为`auto`。

目前支持的语言代码：

*   zh: 中文
    
*   en: 英文
    
*   ja: 日语
    
*   yue: 粤语
    
*   ko: 韩语
    
*   de: 德语
    
*   fr: 法语
    
*   ru: 俄语
    
*   it: 意大利语
    
*   es: 西班牙语
    

transcription\_enabled

boolean

true

否

是否开启识别功能。

注：模型支持识别与翻译功能单独开启或全部开启，但需要至少开启一个能力。

translation\_enabled

boolean

false

否

是否开启翻译功能，注意需`translation_target_languages`有效才能正常输出翻译结果。

translation\_target\_languages

array\[string\]

\-

否

设置翻译目标语言代码。目标语言的代码与`source_language`参数一致。

目前支持的翻译包括：

**中->英，中->日，中->韩，**

**英->中，英->日，英->韩，**

**（日、韩、粤、德、法、俄、意、西）->（中、英）。**

**重要**

目前暂不支持同时翻译为多种语言，请仅设置一个目标语言以完成翻译。

##### 2、发送二进制待识别音频流（单声道）

客户端需在收到`task-started`事件后，再发送待识别的音频流。

可以发送实时音频流（比如从话筒中实时获取到的）或者录音文件音频流，音频应是单声道。

音频通过WebSocket的二进制通道上传。建议每次发送100ms的音频，并间隔100ms。

##### 3、发送finish-task指令：结束语音识别任务

该指令用于结束语音识别任务。音频发送完毕后，客户端可以发送此指令以结束任务。

示例：

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

`payload`参数说明：

**参数**

**类型**

**是否必选**

**说明**

payload.input

object

是

固定格式：{}。

### **四、关闭WebSocket连接**

在程序正常结束、运行中出现异常或接收到`task-finished`、`task-failed`事件时，关闭WebSocket连接。通常通过调用工具库中的`close`函数来实现。

**关于建连开销和连接复用**
---------------

WebSocket服务支持连接复用以提升资源的利用效率，避免建立连接开销。

当服务收到 `run-task` 指令后，将启动一个新的任务，并在任务完成时下发 `task-finished` 指令以结束该任务。结束任务后webSocket连接可以被复用，发送`run-task`指令开启下一个任务。

**重要**

1.  在复用连接中的不同任务需要使用不同 task\_id。
    
2.  如果在任务执行过程中发生失败，服务将依然下发 `task-failed` 指令，并关闭该连接。此时这个连接无法继续复用。
    
3.  如果在任务结束后60秒没有新的任务，连接会超时自动断开。
    

**示例代码**
--------

示例代码仅提供最基础的服务调通实现，实际业务场景的相关代码需您自行开发。

如下示例中，使用的音频文件为[asr\_example.wav](https://help-static-aliyun-doc.aliyuncs.com/file-manage-files/zh-CN/20250218/elsrfb/asr_example.wav)。

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
    	Translations  []Translation `json:"translations"`
    	Transcription Transcription `json:"transcription"`
    }
    
    type Translation struct {
    	SentenceID   int    `json:"sentence_id"`
    	BeginTime    int64  `json:"begin_time"`
    	EndTime      int64  `json:"end_time"`
    	Text         string `json:"text"`
    	Lang         string `json:"lang"`
    	PreEndFailed bool   `json:"pre_end_failed"`
    	Words        []Word `json:"words"`
    	SentenceEnd  bool   `json:"sentence_end"`
    }
    
    type Transcription struct {
    	SentenceID   int    `json:"sentence_id"`
    	BeginTime    int64  `json:"begin_time"`
    	EndTime      int64  `json:"end_time"`
    	Text         string `json:"text"`
    	Words        []Word `json:"words"`
    	SentenceEnd  bool   `json:"sentence_end"`
    }
    
    type Word struct {
    	BeginTime   int64  `json:"begin_time"`
    	EndTime     int64  `json:"end_time"`
    	Text        string `json:"text"`
    	Punctuation string `json:"punctuation"`
    	Fixed       bool   `json:"fixed"`
    	SpeakerID   *int   `json:"speaker_id"`
    }
    
    type Payload struct {
    	TaskGroup  string     `json:"task_group"`
    	Task       string     `json:"task"`
    	Function   string     `json:"function"`
    	Model      string     `json:"model"`
    	Parameters Params     `json:"parameters"`
    	Input      Input      `json:"input"`
    	Output     *Output    `json:"output,omitempty"`
    }
    
    type Params struct {
    	Format                     string   `json:"format"`
    	SampleRate                 int      `json:"sample_rate"`
    	VocabularyID               string   `json:"vocabulary_id,omitempty"`
    	TranslationTargetLanguages []string `json:"translation_target_languages,omitempty"`
    	TranscriptionEnabled       bool     `json:"transcription_enabled,omitempty"`
    	TranslationEnabled         bool     `json:"translation_enabled,omitempty"`
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
    			Model:     "gummy-realtime-v1",
    			Parameters: Params{
    				Format:                     "wav",
    				SampleRate:                 16000,
    				TranscriptionEnabled:       true,
    				TranslationEnabled:         true,
    				TranslationTargetLanguages: []string{"en"},
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
    		fmt.Println("服务器返回结果：")
    		// 解析 Translations 和 Transcription
    		if event.Payload.Output != nil {
    			for _, translation := range event.Payload.Output.Translations {
    				fmt.Printf("	翻译结果 - Sentence ID：%d, Text：%s\n", translation.SentenceID, translation.Text)
    				for _, word := range translation.Words {
    					fmt.Printf("	  Word - Begin Time：%d, End Time：%d, Text：%s\n", word.BeginTime, word.EndTime, word.Text)
    				}
    			}
    
    			transcription := event.Payload.Output.Transcription
    			fmt.Printf("	识别结果 - Sentence ID：%d, Text：%s\n", transcription.SentenceID, transcription.Text)
    			for _, word := range transcription.Words {
    				fmt.Printf("	  Word - Begin Time：%d, End Time：%d, Text：%s\n", word.BeginTime, word.EndTime, word.Text)
    			}
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
    

    using System.Net.WebSockets;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    
    class Program
    {
        private static ClientWebSocket _webSocket = new ClientWebSocket();
        private static readonly CancellationTokenSource _cancellationTokenSource = new();
        // 使用TaskCompletionSource替代轮询等待
        private static TaskCompletionSource<bool> _taskStartedTcs = new();
        private static TaskCompletionSource<bool> _taskFinishedTcs = new();
    
        // 从环境变量获取API密钥（请确保已设置DASHSCOPE_API_KEY）
        // 若没有将API Key配置到环境变量，可将下行替换为：private const string ApiKey="your_api_key"。不建议在生产环境中直接将API Key硬编码到代码中，以减少API Key泄露风险。
        private static readonly string ApiKey = Environment.GetEnvironmentVariable("DASHSCOPE_API_KEY")
            ?? throw new InvalidOperationException("未找到环境变量DASHSCOPE_API_KEY，请先设置API密钥");
        
        // WebSocket服务器地址
        private const string WebSocketUrl = "wss://dashscope.aliyuncs.com/api-ws/v1/inference/";
        // 音频文件路径（请确保文件存在）
        private const string AudioFilePath = "asr_example.wav";
    
        static async Task Main(string[] args)
        {
            Task? receiveTask = null; // Moved outside try block
            try
            {
                Console.WriteLine("开始建立WebSocket连接...");
    
                _webSocket.Options.SetRequestHeader("Authorization", ApiKey);
                _webSocket.Options.SetRequestHeader("X-DashScope-DataInspection", "enable");
    
                await _webSocket.ConnectAsync(new Uri(WebSocketUrl), _cancellationTokenSource.Token);
                Console.WriteLine("WebSocket连接成功");
    
                receiveTask = ReceiveMessagesAsync(); // Assigned to outer variable
    
                var taskId = Guid.NewGuid().ToString("N");
                var runTaskJson = GenerateRunTaskJson(taskId);
                await SendAsync(runTaskJson);
                Console.WriteLine("已发送run-task指令");
    
                if (!await _taskStartedTcs.Task.WaitAsync(TimeSpan.FromSeconds(10)))
                {
                    Console.WriteLine("等待task-started事件超时");
                    _cancellationTokenSource.Cancel();
                    return;
                }
    
                if (!await SendAudioStreamAsync(AudioFilePath))
                {
                    Console.WriteLine("音频流发送失败");
                    return;
                }
    
                var finishTaskJson = GenerateFinishTaskJson(taskId);
                await SendAsync(finishTaskJson);
                Console.WriteLine("已发送finish-task指令");
    
                if (!await _taskFinishedTcs.Task.WaitAsync(TimeSpan.FromSeconds(30)))
                {
                    Console.WriteLine("任务未在规定时间内完成");
                    _cancellationTokenSource.Cancel();
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"发生异常: {ex.Message}");
            }
            finally
            {
                try
                {
                    if (_webSocket.State == WebSocketState.Open)
                    {
                        await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "正常关闭", _cancellationTokenSource.Token);
                        Console.WriteLine("WebSocket连接已关闭");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"关闭连接时出错: {ex.Message}");
                }
                finally
                {
                    _cancellationTokenSource.Cancel();
                    if (receiveTask != null) // Null check added
                    {
                        try
                        {
                            await receiveTask.ConfigureAwait(false); // Safe await with ConfigureAwait
                        }
                        catch (OperationCanceledException)
                        {
                            // Ignore cancellation
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"等待接收任务完成时出错: {ex.Message}");
                        }
                    }
                }
            }
        }
    
        /// <summary>
        /// 接收并处理WebSocket消息
        /// </summary>
        private static async Task ReceiveMessagesAsync()
        {
            try
            {
                while (_webSocket.State == WebSocketState.Open && !_cancellationTokenSource.IsCancellationRequested)
                {
                    var message = await ReceiveMessageAsync(_cancellationTokenSource.Token);
                    if (message != null)
                    {
                        var eventValue = message["header"]?["event"]?.GetValue<string>();
                        switch (eventValue)
                        {
                            case "task-started":
                                Console.WriteLine("收到任务开始事件");
                                _taskStartedTcs.TrySetResult(true);
                                break;
                            case "result-generated":
                                ParseResultGenerated(message);
                                break;
                            case "task-finished":
                                Console.WriteLine("收到任务完成事件");
                                _taskFinishedTcs.TrySetResult(true);
                                break;
                            case "task-failed":
                                Console.WriteLine($"任务失败: {message["header"]?["error_message"]?.GetValue<string>()}");
                                _taskFinishedTcs.TrySetResult(true);
                                _cancellationTokenSource.Cancel();
                                break;
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("消息接收任务已取消");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"接收消息时出错: {ex.Message}");
            }
        }
    
        /// <summary>
        /// 接收单条WebSocket消息
        /// </summary>
        private static async Task<JsonNode?> ReceiveMessageAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[8192]; // 增大缓冲区提高效率
            var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
    
            if (result.MessageType == WebSocketMessageType.Close)
            {
                Console.WriteLine("服务器要求关闭连接");
                return null;
            }
    
            // 处理多段消息拼接
            var messageBuilder = new StringBuilder();
            while (true)
            {
                var segment = new ArraySegment<byte>(buffer, 0, result.Count);
                messageBuilder.Append(Encoding.UTF8.GetString(segment.Array!, segment.Offset, segment.Count));
                
                if (result.EndOfMessage)
                {
                    break;
                }
                
                result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
            }
    
            return JsonNode.Parse(messageBuilder.ToString());
        }
    
        /// <summary>
        /// 发送音频流数据
        /// </summary>
        private static async Task<bool> SendAudioStreamAsync(string filePath)
        {
            var fullPath = Path.Combine(AppContext.BaseDirectory, filePath);
            if (!File.Exists(fullPath))
            {
                Console.WriteLine($"音频文件不存在: {fullPath}");
                return false;
            }
    
            try
            {
                using var audioStream = File.OpenRead(fullPath);
                var buffer = new byte[4096]; // 使用4KB缓冲区
                
                // 计算发送间隔（基于16kHz 16bit mono音频）
                // 16000样本/秒 × 2字节/样本 = 32000字节/秒
                // 4096字节 ≈ 125ms音频数据
                const int intervalMs = 125;
    
                int bytesRead;
                while ((bytesRead = await audioStream.ReadAsync(buffer, 0, buffer.Length, _cancellationTokenSource.Token)) > 0)
                {
                    await _webSocket.SendAsync(new ArraySegment<byte>(buffer, 0, bytesRead), 
                                              WebSocketMessageType.Binary, 
                                              true, 
                                              _cancellationTokenSource.Token);
                    await Task.Delay(intervalMs, _cancellationTokenSource.Token);
                }
                Console.WriteLine("音频流发送完成");
                return true;
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("音频发送被取消");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"音频发送出错: {ex.Message}");
                return false;
            }
        }
    
        /// <summary>
        /// 解析并打印识别结果
        /// </summary>
        private static void ParseResultGenerated(JsonNode message)
        {
            // 提取翻译结果
            var translations = message["payload"]?["output"]?["translations"]?.AsArray();
            if (translations != null)
            {
                foreach (var translation in translations)
                {
                    if (translation == null) continue;
    
                    if (translation["sentence_id"] is JsonValue sentenceIdValue && sentenceIdValue.TryGetValue(out int sentenceId) &&
                        translation["begin_time"] is JsonValue beginTimeValue && beginTimeValue.TryGetValue(out int beginTime) &&
                        translation["end_time"] is JsonValue endTimeValue && endTimeValue.TryGetValue(out int endTime) &&
                        translation["text"] is JsonValue textValue && textValue.TryGetValue(out string? text) &&
                        translation["lang"] is JsonValue langValue && langValue.TryGetValue(out string? lang) &&
                        translation["words"] is JsonArray words)
                    {
                        Console.WriteLine($"\n翻译结果：");
                        Console.WriteLine($"句子ID：{sentenceId}");
                        Console.WriteLine($"时间范围：{beginTime}ms - {endTime}ms");
                        Console.WriteLine($"翻译文本：{text ?? "未知"}");
                        Console.WriteLine($"语言：{lang ?? "未知"}");
    
                        Console.WriteLine("单词级信息：");
                        foreach (var word in words)
                        {
                            if (word == null) continue;
    
                            if (word["begin_time"] is JsonValue wordBeginTimeValue && wordBeginTimeValue.TryGetValue(out int wordBeginTime) &&
                                word["end_time"] is JsonValue wordEndTimeValue && wordEndTimeValue.TryGetValue(out int wordEndTime) &&
                                word["text"] is JsonValue wordTextValue && wordTextValue.TryGetValue(out string? wordText))
                            {
                                Console.WriteLine($"  {wordText} ({wordBeginTime}ms-{wordEndTime}ms)");
                            }
                        }
                    }
                }
            }
    
            // 提取识别结果
            var transcription = message["payload"]?["output"]?["transcription"];
            if (transcription != null)
            {
                if (transcription["sentence_id"] is JsonValue sentenceIdValue && sentenceIdValue.TryGetValue(out int sentenceId) &&
                    transcription["begin_time"] is JsonValue beginTimeValue && beginTimeValue.TryGetValue(out int beginTime) &&
                    transcription["end_time"] is JsonValue endTimeValue && endTimeValue.TryGetValue(out int endTime) &&
                    transcription["text"] is JsonValue textValue && textValue.TryGetValue(out string? text) &&
                    transcription["words"] is JsonArray words)
                {
                    Console.WriteLine($"\n原声识别结果：");
                    Console.WriteLine($"句子ID：{sentenceId}");
                    Console.WriteLine($"时间范围：{beginTime}ms - {endTime}ms");
                    Console.WriteLine($"识别文本：{text ?? "未知"}");
    
                    Console.WriteLine("单词级信息：");
                    foreach (var word in words)
                    {
                        if (word == null) continue;
    
                        if (word["begin_time"] is JsonValue wordBeginTimeValue && wordBeginTimeValue.TryGetValue(out int wordBeginTime) &&
                            word["end_time"] is JsonValue wordEndTimeValue && wordEndTimeValue.TryGetValue(out int wordEndTime) &&
                            word["text"] is JsonValue wordTextValue && wordTextValue.TryGetValue(out string? wordText))
                        {
                            Console.WriteLine($"  {wordText} ({wordBeginTime}ms-{wordEndTime}ms)");
                        }
                    }
                }
            }
        }
    
        /// <summary>
        /// 发送JSON消息到WebSocket
        /// </summary>
        private static async Task SendAsync(string message)
        {
            var buffer = Encoding.UTF8.GetBytes(message);
            var segment = new ArraySegment<byte>(buffer);
            await _webSocket.SendAsync(segment, WebSocketMessageType.Text, true, _cancellationTokenSource.Token);
            Console.WriteLine($"已发送消息: {message.Substring(0, Math.Min(100, message.Length))}...");
        }
    
        /// <summary>
        /// 生成run-task指令的JSON
        /// </summary>
        private static string GenerateRunTaskJson(string taskId)
        {
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
                    ["model"] = "gummy-realtime-v1",
                    ["parameters"] = new JsonObject {
                        ["format"] = "wav",
                        ["sample_rate"] = 16000,
                        ["transcription_enabled"] = true,
                        ["translation_enabled"] = true,
                        ["translation_target_languages"] = new JsonArray {
                            "en"
                        }
                    },
                    ["input"] = new JsonObject()
                }
            };
            return JsonSerializer.Serialize(runTask);
        }
    
        /// <summary>
        /// 生成finish-task指令的JSON
        /// </summary>
        private static string GenerateFinishTaskJson(string taskId)
        {
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
                echo "关闭代码：" . $code . "\n";
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
                "model" => "gummy-realtime-v1",
                "parameters" => [
                    "format" => "wav",
                    "sample_rate" => 16000,
                    "transcription_enabled" => true,
                    "translation_enabled" => true,
                    "translation_target_languages" => ["en"]
                ],
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
                // 解析 result-generated 事件的数据
                $output = $response['payload']['output'];
                $translations = $output['translations'][0];
                $transcription = $output['transcription'];
    
                // 输出翻译结果
                echo "翻译结果：" . $translations['text'] . "\n";
                foreach ($translations['words'] as $word) {
                    echo "文本：" . $word['text'] . ", 开始时间：" . $word['begin_time'] . ", 结束时间：" . $word['end_time'] . "\n";
                }
    
                // 输出转录结果
                echo "转录结果：" . $transcription['text'] . "\n";
                foreach ($transcription['words'] as $word) {
                    echo "文本：" . $word['text'] . ", 开始时间：" . $word['begin_time'] . ", 结束时间：" . $word['end_time'] . "\n";
                }
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
          console.log('服务器返回结果：');
          // 解析payload中的translations
          if (message.payload.output.translations && message.payload.output.translations.length > 0) {
            message.payload.output.translations.forEach(translation => {
              console.log('翻译结果：');
              console.log(`  Sentence ID：${translation.sentence_id}`);
              console.log(`  Begin Time：${translation.begin_time}ms`);
              console.log(`  End Time：${translation.end_time}ms`);
              console.log(`  Text：${translation.text}`);
              console.log(`  Language：${translation.lang}`);
              if (translation.words) {
                console.log('  Words：');
                translation.words.forEach(word => {
                  console.log(`    Begin Time：${word.begin_time}ms`);
                  console.log(`    End Time：${word.end_time}ms`);
                  console.log(`    Text：${word.text}`);
                  console.log(`    Punctuation：${word.punctuation}`);
                  console.log(`    Fixed：${word.fixed}`);
                });
              }
              console.log(`  Pre End Failed：${translation.pre_end_failed}`);
              console.log(`  Sentence End：${translation.sentence_end}`);
            });
          }
    
          // 解析payload中的transcription
          if (message.payload.output.transcription) {
            const transcription = message.payload.output.transcription;
            console.log('识别结果：');
            console.log(`  Sentence ID：${transcription.sentence_id}`);
            console.log(`  Begin Time：${transcription.begin_time}ms`);
            console.log(`  End Time：${transcription.end_time}ms`);
            console.log(`  Text：${transcription.text}`);
            if (transcription.words) {
              console.log('  Words：');
              transcription.words.forEach(word => {
                console.log(`    Begin Time：${word.begin_time}ms`);
                console.log(`    End Time：${word.end_time}ms`);
                console.log(`    Text：${word.text}`);
                console.log(`    Punctuation：${word.punctuation}`);
                console.log(`    Fixed：${word.fixed}`);
              });
            }
            console.log(`  Sentence End：${transcription.sentence_end}`);
          }
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
          model: 'gummy-realtime-v1',
          parameters: {
            sample_rate: 16000,
            format: 'wav',
            transcription_enabled: true,
            translation_enabled: true,
            translation_target_languages: ['en']
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

请参见GitHub [QA](https://github.com/aliyun/alibabacloud-bailian-speech-demo/tree/master/docs/QA)。