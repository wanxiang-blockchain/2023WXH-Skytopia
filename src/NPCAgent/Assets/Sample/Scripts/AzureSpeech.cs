using System;
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Microsoft.CognitiveServices.Speech;

public class AzureSpeech : MonoBehaviour
{
   public AudioSource audioSource;

    //Azure语音服务的秘钥
    [SerializeField]private string SubscriptionKey = "输入微软语音服务的资源秘钥（注册Azure创建服务后获得）";
    //服务所在区域
    [SerializeField]private string Region = "YourServiceRegion";
    //语音设置
    [SerializeField]private string m_SoundSetting="zh-CN-XiaoyiNeural";

    private const int SampleRate = 24000;

    private object threadLocker = new object();
    private bool waitingForSpeak;
    private bool audioSourceNeedStop;
    private string message;

    private SpeechConfig speechConfig;
    private SpeechSynthesizer synthesizer;

    void Start(){
     // Creates an instance of a speech config with specified subscription key and service region.
            speechConfig = SpeechConfig.FromSubscription(SubscriptionKey, Region);

            // The default format is RIFF, which has a riff header.
            // We are playing the audio in memory as audio clip, which doesn't require riff header.
            // So we need to set the format to raw (24KHz for better quality).
            speechConfig.SetSpeechSynthesisOutputFormat(SpeechSynthesisOutputFormat.Raw24Khz16BitMonoPcm);

            //set sound
            speechConfig.SpeechSynthesisVoiceName = m_SoundSetting; 

            // Creates a speech synthesizer.
            // Make sure to dispose the synthesizer after use!
            synthesizer = new SpeechSynthesizer(speechConfig, null);

            synthesizer.SynthesisCanceled += (s, e) =>
            {
                var cancellation = SpeechSynthesisCancellationDetails.FromResult(e.Result);
                message = $"CANCELED:\nReason=[{cancellation.Reason}]\nErrorDetails=[{cancellation.ErrorDetails}]\nDid you update the subscription info?";
            };   
    }
    
    public void SetSound(string _sound){
        //选择一种声音
        m_SoundSetting=_sound;
        speechConfig.SpeechSynthesisVoiceName = m_SoundSetting; 
        synthesizer = new SpeechSynthesizer(speechConfig, null);

        synthesizer.SynthesisCanceled += (s, e) =>
        {
            var cancellation = SpeechSynthesisCancellationDetails.FromResult(e.Result);
            message = $"CANCELED:\nReason=[{cancellation.Reason}]\nErrorDetails=[{cancellation.ErrorDetails}]\nDid you update the subscription info?";
        }; 
    }

    //合成语音并播放
     public void TurnTextToSpeech(string _input)
    {

        if(audioSource.isPlaying){
            audioSource.Stop();
        }

        lock (threadLocker)
        {
            waitingForSpeak = true;
        }

        string newMessage = null;
        var startTime = DateTime.Now;

        // Starts speech synthesis, and returns once the synthesis is started.
        using (var result = synthesizer.StartSpeakingTextAsync(_input).Result)
        {
            // Native playback is not supported on Unity yet (currently only supported on Windows/Linux Desktop).
            // Use the Unity API to play audio here as a short term solution.
            // Native playback support will be added in the future release.
            var audioDataStream = AudioDataStream.FromResult(result);
            var isFirstAudioChunk = true;
            var audioClip = AudioClip.Create(
                "Speech",
                SampleRate * 600, // Can speak 10mins audio as maximum
                1,
                SampleRate,
                true,
                (float[] audioChunk) =>
                {
                    var chunkSize = audioChunk.Length;
                    var audioChunkBytes = new byte[chunkSize * 2];
                    var readBytes = audioDataStream.ReadData(audioChunkBytes);
                    if (isFirstAudioChunk && readBytes > 0)
                    {
                        var endTime = DateTime.Now;
                        var latency = endTime.Subtract(startTime).TotalMilliseconds;
                        newMessage = $"Speech synthesis succeeded!\nLatency: {latency} ms.";
                        isFirstAudioChunk = false;
                    }

                    for (int i = 0; i < chunkSize; ++i)
                    {
                        if (i < readBytes / 2)
                        {
                            audioChunk[i] = (short)(audioChunkBytes[i * 2 + 1] << 8 | audioChunkBytes[i * 2]) / 32768.0F;
                        }
                        else
                        {
                            audioChunk[i] = 0.0f;
                        }
                    }

                    if (readBytes == 0)
                    {
                        Thread.Sleep(200); // Leave some time for the audioSource to finish playback
                        audioSourceNeedStop = true;
                    }
                });

            audioSource.clip = audioClip;
            audioSource.Play();
        }

        lock (threadLocker)
        {
            if (newMessage != null)
            {
                message = newMessage;
            }

            waitingForSpeak = false;
        }
    }


}
