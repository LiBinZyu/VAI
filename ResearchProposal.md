# A Voice Control Pipeline for Unity via Cloud-Streaming ASR, LLM, and Concurrent On-Device Intent Recognition

## Abstract

This research proposes a novel hybrid architecture for voice control in Unity that overcomes the critical trade-off between the low latency of on-device keyword spotting and the high accuracy of cloud-based Natural Language Understanding. Current solutions force developers to choose between limited on-device vocabularies or high-latency cloud services, both of which are ill-suited for real time gaming. This approach resolves this by strategically distributing computational tasks.

**Contribution**:
The primary contribution is an architectural pattern that achieves low-latency, natural language voice control in resource-constrained environments like gaming. By decoupling ASR and NLU and processing them concurrently, eliminate the primary bottleneck of traditional cascaded systems. The components communicate via a structured JSON interface, creating a modular and extensible framework for developers.

The core of our solution is a pipeline that combines two key technologies:

1. **Streaming Voice to Text:** We offload the computationally intensive Automatic Speech Recognition (ASR) task to a high-performance, non-autoregressive cloud service (FunASR Paraformer). This ensures high-accuracy transcription without taxing the user's local machine, making the system universally performant.
2. **Concurrent On-Device Intent Recognition:** To eliminate network latency for language understanding, a lightweight NLU model runs directly on the user's device. Crucially, this model processes the ASR text as it streams in, allowing for near-instantaneous intent and entity recognition, often before the user has finished speaking.
3. **Robustness:** Enable LLM paralled `tool_call` function to handel situations where the NLU failed to detect the vague or complicated intent.

## Introduction & Problem Statement

Voice control in gaming offers a more natural and accessible user interface, but its adoption is hindered by latency. Existing systems present a dilemma:

* **On-Device Systems** (e.g., Unity's KeywordRecognizer, Vosk) are fast but limited to simple, predefined keywords, lacking flexibility.
* **Cloud-Based Systems** (e.g., Meta Voice SDK with Wit.ai) offer powerful NLU but introduce significant network latency, as they must wait for a complete utterance before processing. This delay is unacceptable for real-time gameplay.

This project bridges that gap by creating a hybrid system that leverages the strengths of both approaches while mitigating their weaknesses.

## Proposed System Architecture

The system is a streaming pipeline designed for minimal delay.

<p align="center">
 <img src="https://i.imgur.com/0iU9FPF.png" width="600">
 <p align="left"><b><i>Figure 1:</i></b> The proposed hybrid architecture. Audio is streamed from Unity to the cloud ASR. The resulting text stream is processed concurrently by an on-device NLU model, which generates a structured JSON command to drive in-game actions.
 </p> 
</p>


1. **Voice Activity Detection:** Captures microphone audio and manage voice recording on device.
2. **Speech Recognition:** Streams audio via WebSocket to the Paraformer API. The non-autoregressive model provides a high-speed, real-time stream of partial and final text transcripts.
3. **Intent NLU:** A Chinese text segmentation with customed word matching logic. It continuously parses the incoming text stream to predict intent and extract entities, returns JSON command or redirect to LLM if failed.
4. **LLM**: tool_call based on websocket API using Qwen turbo model, returns JSON command.
5. **Game functions:** The JSON command is passed to the game logic, which translates it into in-game actions.
6. **JSON:** Formatted functions that need to be registered in the module.
    ```json
    // functionRegistryExample.json
    {
        // Unity function indexing
        "Name": "ChangeObjectColor",
        "Description": "Change the color of game object",
        "NameSynonyms": [
            "颜色","color","变成"
        ],
        // Unity function parameters
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

## Plan for Formal Results Section

To validate the system's performance for a formal research paper, the results section will be structured around direct, quantitative comparisons against two baselines: a **Cloud-Only** system (Meta Voice SDK) and an **On-Device-Only** system (Vosk or Unity's KeywordRecognizer).

### Quantitative Performance Tables

The core results will be presented in tables that clearly benchmark the key performance indicators.

| System | WER (%) ↓ | Intent Accuracy (%) ↑ | Entity F1-Score (%) ↑ |  |
| :---- | :---- | :---- | :---- | :---- |
| **Proposed Hybrid System** | (Result) | (Result) | (Result) |  |
| Cloud-Only Baseline | (Result) | (Result) | (Result) |  |
| On-Device-Only Baseline | (Result) | N/A | N/A |  |

Table 1: ASR and NLU Accuracy Comparison. Word Error Rate (WER) measures transcription accuracy, while Intent Accuracy and F1-Score measure NLU performance. The On-Device baseline is not applicable for complex NLU.

| System | End-to-End Latency (ms) ↓ | Time-to-Intent (ms) ↓ |  |
| :---- | :---- | :---- | :---- |
| **Proposed Hybrid System** | (Result) | (Result) |  |
| Cloud-Only Baseline | (Result) | (Result) |  |
| On-Device-Only Baseline | (Result) | (Result) |  |

Table 2: Latency Comparison. End-to-End Latency is the time from end-of-speech to action execution. Time-to-Intent measures the time from the final keyword utterance to NLU completion, highlighting the advantage of our concurrent approach.

### Visualizations of Latency and Resource Usage

To make the performance benefits intuitive, the results will be visualized using graphs.

* **Latency Comparison Chart:** A bar chart will be used to visually compare the average End-to-End Latency and Time-to-Intent across the three systems. This will provide a clear, immediate illustration of our system's responsiveness advantage.  
* **Resource Footprint Graph:** Using the Unity Profiler, we will capture the CPU and Memory usage of our on-device NLU component during a gameplay session. This will be presented as a line graph over time, plotted against the application's frame rate (FPS). The goal is to demonstrate that the NLU process has a negligible impact on game performance, allowing the application to maintain its target FPS (e.g., 60 FPS).

### Analysis of Results

The final part of the results section will analyze and interpret the data presented in the tables and graphs. This analysis will explicitly connect the quantitative findings back to the project's central hypothesis: that a hybrid, concurrent architecture can achieve the natural language flexibility of a cloud system at a latency profile approaching that of a simple on-device system, thus solving a core problem for voice interaction in real-time applications.