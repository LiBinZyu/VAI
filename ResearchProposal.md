# Abstract

This research proposes a novel hybrid architecture for voice control in Unity that overcomes the critical trade-off between the low latency of on-device keyword spotting and the high accuracy of cloud-based Natural Language Understanding. Current solutions force developers to choose between limited on-device vocabularies or high-latency cloud services, both of which are ill-suited for real time gaming. This approach resolves this by strategically distributing computational tasks.

**Contribution**:
The primary contribution is an architectural pattern that achieves low-latency, natural language voice control in resource-constrained environments like gaming. By decoupling ASR and NLU and processing them concurrently, eliminate the primary bottleneck of traditional cascaded systems. The components communicate via a structured JSON interface, creating a modular and extensible framework for developers.

The core of our solution is a pipeline that combines two key technologies:

1. **Streaming Voice to Text:** We offload the computationally intensive Automatic Speech Recognition (ASR) task to a high-performance, non-autoregressive cloud service (FunASR Paraformer). This ensures high-accuracy transcription without taxing the user's local machine, making the system universally performant.
2. **Concurrent On-Device Intent Recognition:** To eliminate network latency for language understanding, a lightweight NLU model runs directly on the user's device. Crucially, this model processes the ASR text as it streams in, allowing for near-instantaneous intent and entity recognition, often before the user has finished speaking.
3. **Robustness:** Enable LLM paralled `tool_call` function to handel situations where the NLU failed to detect the vague or complicated intent.

# Problem Statement

Voice control in gaming promises a natural and accessible user interface, but its adoption is limited by latency and flexibility. On-device systems (e.g., Unity's KeywordRecognizer, Vosk) are fast but restricted to simple, predefined keywords, lacking the flexibility for natural language. Cloud-based systems (e.g., Meta Voice SDK with Wit.ai) offer powerful natural language understanding (NLU) but introduce significant network latency, as they require a complete utterance before processing. This delay is unacceptable for real-time gameplay. Current solutions force developers to choose between limited on-device vocabularies or high-latency cloud services, both of which are ill-suited for real-time gaming.

<p align="center">
 <img src="https://i.imgur.com/0iU9FPF.png " width="600">
 <p align="left"><b><i>Figure 1:</i></b> The proposed hybrid architecture. Audio is streamed from Unity to the cloud ASR. The resulting text stream is processed concurrently by an on-device NLU model, which generates a structured JSON command to drive in-game actions.
 </p> 
</p>

# Aim and Objectives

**Aim:**  
To develop a hybrid voice control pipeline for Unity that achieves both low-latency and high-accuracy natural language understanding by combining cloud-based ASR with concurrent on-device intent recognition.

**Objectives:**
1. Design and implement a streaming pipeline that offloads Automatic Speech Recognition (ASR) to a high-performance cloud service while running lightweight NLU on-device.
2. Enable concurrent processing of ASR text streams and on-device intent recognition to minimize end-to-end latency.
3. Integrate a fallback mechanism using LLM-based intent recognition for cases where on-device NLU fails.
4. Validate the system's performance through quantitative comparison with cloud-only and on-device-only baselines.

# Project Plan and Expected Outcome

**Objective 1:**  
*Plan:* Develop a modular pipeline where Unity streams audio to a cloud ASR (e.g., FunASR Paraformer) and receives real-time text transcripts.  
*Expected Outcome:* A Unity-compatible system that reliably transcribes speech to text with high accuracy and minimal local resource usage.

**Objective 2:**  
*Plan:* Implement on-device NLU that processes the ASR text stream in real time, extracting intent and entities as the user speaks.  
*Expected Outcome:* Near-instantaneous intent recognition, enabling responsive in-game actions with minimal latency.

**Objective 3:**  
*Plan:* Integrate an LLM-based (e.g., Qwen turbo) fallback via websocket API, triggered when the on-device NLU cannot resolve ambiguous or complex intents.  
*Expected Outcome:* Robust handling of vague or complicated user commands, ensuring high intent recognition coverage.

**Objective 4:**  
*Plan:* Benchmark the hybrid system against cloud-only and on-device-only baselines using metrics such as Word Error Rate (WER), Intent Accuracy, Entity F1-Score, and latency. Visualize results with tables and charts, and analyze resource usage with the Unity Profiler.  
*Expected Outcome:* Demonstrated superiority of the hybrid approach in both accuracy and latency, with negligible impact on game performance.