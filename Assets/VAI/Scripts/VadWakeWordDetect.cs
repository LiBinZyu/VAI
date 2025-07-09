using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI; // 若使用TMP则改为 using TMPro;
using UnityEngine.Windows.Speech;
using System.Linq;
using UnityEngine.Events;

public class VadWakeWordDetect : MonoBehaviour
{
    [Header("Windows Voice Detection Settings")]
    [Tooltip("Under this value is recognized as silence")]
    public float volumeThreshold = 0.03f;
    [Tooltip("After this timespan of silence send audio recording to ASR (in second)")]
    public float silenceThreshold = 0.5f;
    [Tooltip("Delay(buffer) after wake words detected")]
    public float listeningDelay = 1.5f;
    
    [Header("Wake Words")]
    [Tooltip("Wake word you want to detect in Windows microphone")]
    public List<string> keywords = new List<string> { "Hi there" };
    public ConfidenceLevel confidenceLevel = ConfidenceLevel.High;// Detection confidence is set to high by default 

    [Header("On wake word detected")]
    public Text showText;
    
    [Header("Events")]
    public UnityEvent OnWakeWordRecognized;

    private KeywordRecognizer keywordRecognizer;
    private Dictionary<string, Action> keywordActions;
    
    public AudioClip MicrophoneClip { get; private set; }
    public string MicrophoneDevice { get; private set; }
    private float[] clipSampleData = new float[1024];

    void Start()
    {
        if (Microphone.devices.Length > 0)
        {
            MicrophoneDevice = Microphone.devices[0];
            MicrophoneClip = Microphone.Start(MicrophoneDevice, true, 10, 16000); 
            Debug.Log($"Microphone started on {MicrophoneDevice} by VadWakeWordDetect.");
        }
        else
        {
            Debug.LogError("No available microphone.");
            return; // Stop execution if no mic
        }
        
        // 初始化关键词字典
        keywordActions = new Dictionary<string, Action>();
        foreach (string keyword in keywords)
        {
            keywordActions.Add(keyword, () => { });
        }

        // 创建 KeywordRecognizer 实例
        keywordRecognizer = new KeywordRecognizer(keywordActions.Keys.ToArray(), confidenceLevel);

        // 注册回调
        keywordRecognizer.OnPhraseRecognized += OnPhraseRecognized;
        keywordRecognizer.Start();
        Debug.Log("关键词识别器已启动。");
    }

    // 获取当前麦克风信号强度
    // [FIXED] 修正了获取麦克风实时音量的逻辑
    public float GetMicrophoneVolume()
    {
        if (MicrophoneClip == null || !Microphone.IsRecording(MicrophoneDevice))
        {
            return 0f;
        }

        // 获取麦克风在环形缓冲区中的当前写入位置
        int micPosition = Microphone.GetPosition(MicrophoneDevice);
        int sampleCount = clipSampleData.Length;

        // 我们想要读取的是写入位置 *之前* 的一段数据。
        // 因此，计算读取的起始位置。
        int readStartPosition = micPosition - sampleCount;

        // 处理环形缓冲区的回绕（wrap-around）情况。
        // 如果计算出的起始位置为负，说明数据块跨越了缓冲区的末尾和开头。
        // Unity的GetData不支持一次性读取回绕的数据。为简单起见，在这种情况下，
        // 我们从缓冲区开头读取，这依然能提供一个足够准确的音量近似值。
        if (readStartPosition < 0)
        {
            readStartPosition = 0;
        }

        // 从计算好的、安全的位置读取最新的音频样本
        MicrophoneClip.GetData(clipSampleData, readStartPosition);

        // 通过计算样本的均方根（RMS）来得到音量值
        float sumOfSquares = 0f;
        foreach (float sample in clipSampleData)
        {
            sumOfSquares += sample * sample; // 求平方和
        }
        
        // 返回均方根
        return Mathf.Sqrt(sumOfSquares / sampleCount);
    }
    
    void OnPhraseRecognized(PhraseRecognizedEventArgs args)
    {
        Debug.Log($"detected text: {args.text} (confidence: {args.confidence})");

        if (keywordActions.ContainsKey(args.text))
        {
            OnWakeWordRecognized?.Invoke();
            keywordActions[args.text].Invoke();
            showText.text = args.text+"...";
        }
    }
    
    private void OnDestroy()
    {
        if (keywordRecognizer != null && keywordRecognizer.IsRunning)
        {
            keywordRecognizer.Stop();
            keywordRecognizer.OnPhraseRecognized -= OnPhraseRecognized;
            keywordRecognizer.Dispose();
        }
        if (Microphone.IsRecording(MicrophoneDevice))
        {
            Microphone.End(MicrophoneDevice);
        }
    }
}