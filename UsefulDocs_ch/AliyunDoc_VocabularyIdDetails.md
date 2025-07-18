针对您的特定业务领域，如果有部分词汇的语音识别效果不够好，可以将这些关键词或短语添加为热词进行优先识别，从而提升识别效果。

## 热词简介

热词通过热词列表的形式在SDK中使用，热词列表是JSON列表，其中每一个热词包含如下字段：

| **字段** | **类型** | **是否必填** | **说明** |
| --- | --- | --- | --- |

|     |     |     |     |
| --- | --- | --- | --- |
| **字段** | **类型** | **是否必填** | **说明** |
| text | string | 是   | 热词文本，每个词语最长10个汉字或英文单词。 |
| weight | int | 是   | 热词权重，取值范围为\[1, 5\]之间的整数。<br><br>常用值：4。<br><br>如果效果不明显可以适当增加权重，但是当权重较大时可能会引起负面效果，导致其他词语识别不准确。 |
| lang | string | 否   | 语言代码，支持对ASR模型对应的语种做热词加强。具体语种和语言代码对应关系请参考模型的API详情页。请在调用语音识别服务使用指定同样的语种，如果指定识别语种language\_hints后其他语种热词会失效。 |

## 应用场景

为了提高电影名称的识别率，将如下电影名称作为热词添加到项目中。

```json
[
    {"text": "赛德克巴莱", "weight": 4, "lang": "zh"},
    {"text": "Seediq Bale", "weight": 4, "lang": "en"},
    
    {"text": "夏洛特烦恼", "weight": 4, "lang": "zh"},
    {"text": "Goodbye Mr. Loser", "weight": 4, "lang": "en"},
    
    {"text": "阙里人家", "weight": 4, "lang": "zh"},
    {"text": "Confucius' Family", "weight": 4, "lang": "en"},
]
```

## 支持的模型列表

*   实时语音识别：paraformer-realtime-v2，paraformer-realtime-8k-v2
    
*   录音文件识别：paraformer-v2，paraformer-8k-v2
    

## 计费说明

热词暂不收费。

## 前提条件

*   已开通服务并获得API-KEY：[获取API Key](https://help.aliyun.com/zh/model-studio/get-api-key)。
    
*   我们推荐您将API-KEY配置到环境变量中以降低API-KEY的泄漏风险，详情可参考[配置API Key到环境变量](https://help.aliyun.com/zh/model-studio/configure-api-key-through-environment-variables)。您也可以在代码中配置API-KEY，但是泄漏风险会提高。
    
*   已安装最新版SDK：[安装SDK](https://help.aliyun.com/zh/model-studio/install-sdk/)。
    

**注意：**

**1、**每个热词列表最多添加500个词，

**2、**每个账号默认10个（Paraformer和Gummy共用）热词列表，如果有需要创建更多请联系我们。

## 代码示例

以下示例代码展示如何创建一个热词表，并通过实时接口调用paraformer-realtime-v2模型识别本地音频文件。

Python

Java

Python

```python
import dashscope
from dashscope.audio.asr import *


dashscope.api_key = 'your-dashscope-api-key'
prefix = 'prefix'
target_model = "paraformer-realtime-v2"

my_vocabulary = [
    {"text": "吴贻弓", "weight": 4, "lang": "zh"},
    {"text": "阙里人家", "weight": 4, "lang": "zh"},
]


# create a vocabulary
service = VocabularyService()
vocabulary_id = service.create_vocabulary(
      prefix=prefix,
      target_model=target_model,
      vocabulary=my_vocabulary)

print(f"your vocabulary id is {vocabulary_id}")

# use the vocabulary to recognize a file
recognition = Recognition(model=target_model,
                          format='wav',
                          sample_rate=16000,
                          callback=None,
                          vocabulary_id=vocabulary_id,
                          language_hints=['zh'])  # “language_hints”只支持paraformer-v2和paraformer-realtime-v2模型
result = recognition.call('your-audio-file.wav')
print(result.output)
```

Java

```java
package org.example.customization;

import com.alibaba.dashscope.audio.asr.recognition.Recognition;
import com.alibaba.dashscope.audio.asr.recognition.RecognitionParam;
import com.alibaba.dashscope.audio.asr.vocabulary.Vocabulary;
import com.alibaba.dashscope.audio.asr.vocabulary.VocabularyService;
import com.alibaba.dashscope.exception.InputRequiredException;
import com.alibaba.dashscope.exception.NoApiKeyException;
import com.google.gson.JsonArray;
import com.google.gson.JsonObject;

import java.io.File;
import java.util.ArrayList;
import java.util.List;

public class VocabularySampleCodes {
    public static String apiKey = "your-dashscope-apikey";

    public static void main(String[] args) throws NoApiKeyException, InputRequiredException {
        String targetModel = "paraformer-realtime-v2";
        // prepare vocabulary
        class Hotword {
            String text;
            int weight;
            String lang;

            public Hotword(String text, int weight, String lang) {
                this.text = text;
                this.weight = weight;
                this.lang = lang;
            }
        }
        JsonArray vocabulary = new JsonArray();
        List<Hotword> wordList = new ArrayList<>();
        wordList.add(new Hotword("吴贻弓", 4, "zh"));
        wordList.add(new Hotword("阙里人家", 4, "zh"));

        for (Hotword word : wordList) {
            JsonObject jsonObject = new JsonObject();
            jsonObject.addProperty("text", word.text);
            jsonObject.addProperty("weight", word.weight);
            jsonObject.addProperty("lang", word.lang);
            vocabulary.add(jsonObject);
        }
        // create vocabulary
        VocabularyService service = new VocabularyService(apiKey);
        Vocabulary myVoc = service.createVocabulary(targetModel, "prefix", vocabulary);
        System.out.println("your vocabulary id is " + myVoc.getVocabularyId());
        // use the vocabulary to recognize a file
        Recognition recognizer = new Recognition();
        RecognitionParam param =
                RecognitionParam.builder()
                        .model(targetModel)
                        .format("wav")
                        .sampleRate(16000)
                        .apiKey(apiKey)
                        .vocabularyId(myVoc.getVocabularyId())
                        // “language_hints”只支持paraformer-v2和paraformer-realtime-v2模型
                        .parameter("language_hints", new String[] {"zh"})
                        .build();
        String result = recognizer.call(param, new File("your-local-audio-file.wav"));
        System.out.println(result);
        System.exit(0);
    }
}
```

## **使用DashScope SDK增删查改热词**

### 初始化对象

Python

Java

Python

```python
service = VocabularyService(api_key="your-dashscope-apikey")
```

Java

```java
VocabularyService service = new VocabularyService("your-dashscope-apikey");
```

### 创建热词表

Python

Java

Python

```python
def create_vocabulary(self, target_model: str, prefix: str, vocabulary: List[dict]) -> str:
    '''
    创建热词表
    param: target_model 热词表对应的语音识别模型版本
    param: prefix 热词表自定义前缀，仅允许数字和小写字母，小于十个字符。
    param: vocabulary 热词表字典
    return: 热词表标识符 vocabulary_id
    '''
```

Java

```java
/**
 * 创建新热词
 *
 * @param targetModel 热词对应的语音识别模型版本
 * @param prefix 热词自定义前缀，仅允许数字和小写字母，小于十个字符。
 * @param vocabulary 热词表
 * @return 热词表对象
 * @throws NoApiKeyException 如果apikey为空
 * @throws InputRequiredException 如果必须参数为空
 */
public Vocabulary createVocabulary(String targetModel, String prefix, JsonArray vocabulary)
    throws NoApiKeyException, InputRequiredException 
```

### 查询所有热词表

Python

Java

Python

```python
def list_vocabularies(self, prefix=None, page_index: int = 0, page_size: int = 10) -> List[dict]:
    '''
    查询已创建的所有热词表
    param: prefix 自定义前缀，如果设定则只返回指定前缀的热词表标识符列表。
    param: page_index 查询的页索引
    param: page_size 查询页大小
    return: 热词表标识符列表
    '''
```

Java

```java
/**
 * 查询已创建的所有热词表。默认的页索引为0，默认的页大小为10
 *
 * @param prefix 热词自定义前缀
 * @return 热词表对象数组
 * @throws NoApiKeyException 如果apikey为空
 * @throws InputRequiredException 如果必须参数为空
 */
public Vocabulary[] listVocabulary(String prefix)
    throws NoApiKeyException, InputRequiredException

/**
 * 查询已创建的所有热词表
 *
 * @param prefix 热词自定义前缀
 * @param pageIndex 查询的页索引
 * @param pageSize 查询的页大小
 * @return 热词表对象数组
 * @throws NoApiKeyException 如果apikey为空
 * @throws InputRequiredException 如果必须参数为空
 */
public Vocabulary[] listVocabulary(String prefix, int pageIndex, int pageSize)
    throws NoApiKeyException, InputRequiredException
```

响应示例

```json
[
    {
        "gmt_create": "2024-08-21 15:19:09",
        "vocabulary_id": "vocab-xxx-1f8b10e61ac54b1da86a8d5axxxxxxxx",
        "gmt_modified": "2024-08-21 15:19:09",
        "status": "OK",
    },
    {
        "gmt_create": "2024-08-27 11:17:04",
        "vocabulary_id": "vocab-xxx-24ee19fa8cfb4d52902170a0xxxxxxxx",
        "gmt_modified": "2024-08-27 11:17:04",
        "status": "OK",
    }
]
```

### 查询指定热词表

Python

Java

Python

```python
def query_vocabulary(self, vocabulary_id: str) -> List[dict]:
    '''
    获取热词表内容
    param: vocabulary_id 热词表标识符
    return: 热词表
    '''
```

Java

```java
/**
 * 查询指定热词表
 *
 * @param vocabularyId 需要查询的热词表
 * @return 热词表对象
 * @throws NoApiKeyException 如果apikey为空
 * @throws InputRequiredException 如果必须参数为空
 */
public Vocabulary queryVocabulary(String vocabularyId)
    throws NoApiKeyException, InputRequiredException
```

响应示例

```json
{
    "gmt_create": "2024-08-21 15:19:09",
    "vocabulary": [
        {"weight": 4, "text": "吴贻弓", "lang": "zh"},
        {"weight": 4, "text": "阙里人家", "lang": "zh"},
    ],
    "target_model": "paraformer-realtime-v2",
    "gmt_modified": "2024-08-21 15:19:09",
    "status": "OK",
}
```

### 更新热词表

Python

Java

Python

```python
def update_vocabulary(self, vocabulary_id: str, vocabulary: List[dict]) -> None:
    '''
    用新的热词表替换已有热词表
    param: vocabulary_id 需要替换的热词表标识符
    param: vocabulary 热词表
    '''
```

Java

```java
/**
 * 更新热词表
 *
 * @param vocabularyId 需要更新的热词表
 * @param vocabulary 热词表对象
 * @throws NoApiKeyException 如果apikey为空
 * @throws InputRequiredException 如果必须参数为空
 */
public void updateVocabulary(String vocabularyId, JsonArray vocabulary)
    throws NoApiKeyException, InputRequiredException
```

### 删除热词表

Python

Java

Python

```python
def delete_vocabulary(self, vocabulary_id: str) -> None:
    '''
    删除热词表
    param: vocabulary_id 需要删除的热词表标识符
    '''
```

Java

```java
/**
 * 删除热词表
 *
 * @param vocabularyId 需要删除的热词表
 * @throws NoApiKeyException 如果apikey为空
 * @throws InputRequiredException 如果必须参数为空
 */
public void deleteVocabulary(String vocabularyId)
    throws NoApiKeyException, InputRequiredException
```

### 错误处理

在 Python SDK 中调用发生错误将通过异常 VocabularyServiceException 抛出。异常包括：状态码、异常代码、异常描述

```python
class VocabularyServiceException(Exception):
  def __init__(self, status_code: int, code: str, error_message: str)
```

在 Java SDK 中调用发生错误将通过 NoApiKeyException 和 InputRequiredException异常抛出。

## **通过HTTP服务增删查改热词**

热词服务的协议为HTTPS，开发者可以通过HTTP创建、查询、更新、删除热词表。

如您没将API Key配置到环境变量中，请将cURL中的“Authorization: Bearer $DASHSCOPE\_API\_KEY”改成Authorization: Bearer your-api-key”，其中your-api-key为您的API Key。

**说明**

返回结果中的 usage 字段用于计费计数。定制热词服务是免费的，因此您无需关注该字段。

### **创建热词表**

curl调用示例：

```curl
curl -X POST https://dashscope.aliyuncs.com/api/v1/services/audio/asr/customization \
-H "Authorization: Bearer $DASHSCOPE_API_KEY" \
-H "Content-Type: application/json" \
-d '{
    "model": "speech-biasing", //该参数为创建热词时的固定参数，不对应实际公开模型
    "input": {
            "action": "create_vocabulary",
            "target_model": "paraformer-realtime-v2",
            "prefix": "testpfx",
            "vocabulary": [
              {"text": "吴贻弓", "weight": 4, "lang": "zh"},
              {"text": "阙里人家", "weight": 4, "lang": "zh"}
            ]
        }
}'
```

返回示例：

```json
{
  "output": {
    "task_status": "PENDING",
    "task_id": "c2e5d63b-96e1-4607-bb91-************"
  },
  "request_id": "77ae55ae-be17-97b8-9942--************""
}
```

### **查询所有热词表**

curl调用示例：

```curl
curl -X POST https://dashscope.aliyuncs.com/api/v1/services/audio/asr/customization \
-H "Authorization: Bearer $DASHSCOPE_API_KEY" \
-H "Content-Type: application/json" \
-d '{
    "model": "speech-biasing",//该参数为创建热词时的固定参数，不对应实际公开模型
    "input": {
                "action": "list_vocabulary",
                "prefix": null,
                "page_index": 0,
                "page_size": 10
            }
}'
```

**说明**

prefix字段在示例中为null，会返回所有热词表，您可以根据需要修改为指定字符串。

返回示例：

```json
{
	"output": {
		"vocabulary_list": [{
			"gmt_create": "2024-11-05 16:31:32",
			"vocabulary_id": "vocab-testpfx-6977ae49f65c4c3db054727cxxxxxxxx",
			"gmt_modified": "2024-11-05 16:31:32",
			"status": "OK"
		}]
	},
	"usage": {
		"count": 1
	},
	"request_id": "4e7df7c0-18a8-9f3e-bfc4-xxxxxxxxxxxx"
}
```

### **查询指定热词表**

curl调用示例：

```curl
curl -X POST https://dashscope.aliyuncs.com/api/v1/services/audio/asr/customization \
-H "Authorization: Bearer $DASHSCOPE_API_KEY" \
-H "Content-Type: application/json" \
-d '{
    "model": "speech-biasing",//该参数为创建热词时的固定参数，不对应实际公开模型
    "input": {
                "action": "query_vocabulary",
                "vocabulary_id": "vocab-testpfx-6977ae49f65c4c3db054727cxxxxxxxx"
            }
}'
```

返回示例：

```json
{
	"output": {
		"gmt_create": "2024-11-05 16:31:32",
		"vocabulary": [{
			"weight": 4,
			"text": "吴贻弓",
			"lang": "zh"
		}, {
			"weight": 4,
			"text": "阙里人家",
			"lang": "zh"
		}],
		"target_model": "paraformer-realtime-v2",
		"gmt_modified": "2024-11-05 16:31:32",
		"status": "OK"
	},
	"usage": {
		"count": 1
	},
	"request_id": "b02d18a4-ff8d-9fd4-b4f0-xxxxxxxxxxxx"
}
```

### **更新热词表**

curl调用示例：

```curl
curl -X POST https://dashscope.aliyuncs.com/api/v1/services/audio/asr/customization \
-H "Authorization: Bearer $DASHSCOPE_API_KEY" \
-H "Content-Type: application/json" \
-d '{
    "model": "speech-biasing",//该参数为创建热词时的固定参数，不对应实际公开模型
    "input": {
                "action": "update_vocabulary",
                "vocabulary_id": "vocab-testpfx-6977ae49f65c4c3db054727cxxxxxxxx",
                "vocabulary": [
                  {"text": "吴贻弓", "weight": 4, "lang": "zh"}
                ]      
            }
}'
```

返回示例：

```json
{
	"output": {},
	"usage": {
		"count": 1
	},
	"request_id": "a51f3139-7aaa-941b-994f-xxxxxxxxxxxx"
}
```

### **删除热词表**

curl调用示例：

```curl
curl -X POST https://dashscope.aliyuncs.com/api/v1/services/audio/asr/customization \
-H "Authorization: Bearer $DASHSCOPE_API_KEY" \
-H "Content-Type: application/json" \
-d '{
    "model": "speech-biasing",//该参数为创建热词时的固定参数，不对应实际公开模型
    "input": {
                "action": "delete_vocabulary",
                "vocabulary_id": "vocab-testpfx-6977ae49f65c4c3db054727cxxxxxxxx"
            }
}'
```

返回示例：

```json
{
	"output": {},
	"usage": {
		"count": 1
	},
	"request_id": "d7499ee5-6c91-956c-a1aa-xxxxxxxxxxxx"
}
```

## **错误码说明**

| **HTTP返回码** | **错误代码 Code** | **错误信息 Message**<br><br>**（具体信息内容可能跟随场景有所变化）** | **含义说明** |
| --- | --- | --- | --- |

|     |     |     |     |
| --- | --- | --- | --- |
| **HTTP返回码** | **错误代码 Code** | **错误信息 Message**<br><br>**（具体信息内容可能跟随场景有所变化）** | **含义说明** |
| 400 | Throttling.AllocationQuota | Free allocated quota exceeded. | 热词数目已超过上限。<br><br>每个账号默认10个（Paraformer和Gummy共用）热词列表，您可以删除一部分热词，或通过提交[阿里云工单](https://smartservice.console.aliyun.com/service/create-ticket?spm=a2c4g.2667824.0.0.6a2f6f83Ivpy5F)、加入[开发者群](https://github.com/aliyun/alibabacloud-bailian-speech-demo)联系我们，申请扩容。 |
| 416 | BadRequest.ResourceNotExist | The Required resource not exist. | 更新，查询或删除接口调用时热词资源不存在。 |

更多通用错误码的详细信息，请参阅[错误信息](https://help.aliyun.com/zh/model-studio/error-code)。

[上一篇：RESTful API](/zh/model-studio/paraformer-recorded-speech-recognition-restful-api)[下一篇：最佳实践](/zh/model-studio/developer-reference/paraformer-best-practices)