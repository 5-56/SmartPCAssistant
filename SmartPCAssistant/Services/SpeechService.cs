using System;
using System.IO;
using Serilog;

namespace SmartPCAssistant.Services;

public interface ISpeechService
{
    Task<string> RecognizeFromMicrophoneAsync(CancellationToken cancellationToken = default);
    Task SpeakAsync(string text, CancellationToken cancellationToken = default);
    void SetLanguage(string language);
    bool IsMicrophoneAvailable();
    bool IsSpeakerAvailable();
}

public class SpeechService : ISpeechService
{
    private static SpeechService? _instance;
    public static SpeechService Instance => _instance ??= new SpeechService();

    private string _language = "zh-CN";
    private bool _isInitialized;

    private SpeechService() { }

    public void SetLanguage(string language)
    {
        _language = language;
        Log.Information("Speech language set to: {Language}", language);
    }

    public bool IsMicrophoneAvailable()
    {
        try
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        }
        catch
        {
            return false;
        }
    }

    public bool IsSpeakerAvailable()
    {
        try
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> RecognizeFromMicrophoneAsync(CancellationToken cancellationToken = default)
    {
        Log.Information("Starting speech recognition...");

        try
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                await Task.Delay(1000, cancellationToken);
                Log.Warning("Speech recognition not available on this platform");
                return "语音识别需要在 Windows 上运行";
            }

            var result = await WindowsSpeechRecognitionAsync(cancellationToken);
            Log.Information("Speech recognition result: {Result}", result);
            return result;
        }
        catch (OperationCanceledException)
        {
            Log.Information("Speech recognition cancelled");
            return string.Empty;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Speech recognition failed");
            throw;
        }
    }

    public async Task SpeakAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        Log.Information("Speaking: {Text}", text.Length > 50 ? text[..50] + "..." : text);

        try
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                await Task.Delay(100, cancellationToken);
                return;
            }

            await WindowsSpeechSynthesisAsync(text, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            Log.Information("Speech cancelled");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Speech synthesis failed");
        }
    }

    private Task<string> WindowsSpeechRecognitionAsync(CancellationToken cancellationToken)
    {
#if WINDOWS
        return Task.Run(async () =>
        {
            try
            {
                var assemblyPath = Path.GetDirectoryName(typeof(SpeechService).Assembly.Location);
                var tempFile = Path.Combine(Path.GetTempPath(), $"speech_{Guid.NewGuid()}.wav");

                await using var stream = new FileStream(tempFile, FileMode.Create);
                await CaptureMicrophoneAsync(stream, cancellationToken);

                return await ConvertSpeechToTextAsync(tempFile, cancellationToken);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Windows speech recognition failed");
                return $"语音识别失败: {ex.Message}";
            }
        }, cancellationToken);
#else
        return Task.FromResult("语音识别需要在 Windows 上运行");
#endif
    }

    private Task WindowsSpeechSynthesisAsync(string text, CancellationToken cancellationToken)
    {
#if WINDOWS
        return Task.Run(() =>
        {
            try
            {
                using var synthesizer = new System.Speech.Synthesis.SpeechSynthesizer();
                synthesizer.SetOutputToDefaultAudioDevice();
                synthesizer.Rate = 0;
                synthesizer.Volume = 100;

                var builder = new System.Speech.Synthesis.PromptBuilder();
                builder.AppendText(text);
                synthesizer.SpeakAsyncCancelAll();

                var prompt = builder.ToPrompt();
                synthesizer.SpeakAsync(prompt);

                while (synthesizer.State == System.Speech.Synthesis.SynthesizerState.Speaking && !cancellationToken.IsCancellationRequested)
                {
                    Thread.Sleep(100);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Windows speech synthesis failed");
            }
        }, cancellationToken);
#else
        return Task.CompletedTask;
#endif
    }

    private Task CaptureMicrophoneAsync(Stream outputStream, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            try
            {
                var sampleRate = 16000;
                var channels = 1;
                var bitsPerSample = 16;
                var byteRate = sampleRate * channels * bitsPerSample / 8;

                using var writer = new BinaryWriter(outputStream);
                writer.Write(new[] { 'R', 'I', 'F', 'F' });
                writer.Write(0);
                writer.Write(new[] { 'W', 'A', 'V', 'E' });
                writer.Write(new[] { 'f', 'm', 't', ' ' });
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)channels);
                writer.Write(sampleRate);
                writer.Write(byteRate);
                writer.Write((short)(channels * bitsPerSample / 8));
                writer.Write((short)bitsPerSample);
                writer.Write(new[] { 'd', 'a', 't', 'a' });
                writer.Write(0);

                Log.Information("Microphone capture would start here on Windows");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Microphone capture failed");
            }
        }, cancellationToken);
    }

    private Task<string> ConvertSpeechToTextAsync(string audioFile, CancellationToken cancellationToken)
    {
        return Task.FromResult("语音输入功能需要在 Windows 上配置 Speech API");
    }
}

public static class RuntimeInformation
{
    public static bool IsOSPlatform(OSPlatform platform) => platform == OSPlatform.Windows;
}

public enum OSPlatform
{
    Windows,
    Linux,
    FreeBSD,
    macOS
}
