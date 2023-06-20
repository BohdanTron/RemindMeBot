using Concentus.Oggfile;
using Concentus.Structs;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using NAudio.Wave;

namespace RemindMeBot.Services
{
    public interface ISpeechTranscriptionService
    {
        Task<string?> Transcribe(string audioType, string audioFileUrl, string language);
    }

    public class AzureSpeechTranscriptionService : ISpeechTranscriptionService
    {
        private readonly HttpClient _httpClient;

        private readonly string _subscriptionKey;
        private readonly string _region;

        public AzureSpeechTranscriptionService(HttpClient httpClient, string subscriptionKey, string region)
        {
            _httpClient = httpClient;
            _subscriptionKey = subscriptionKey;
            _region = region;
        }

        public async Task<string?> Transcribe(string audioType, string audioFileUrl, string language)
        {
            if (audioType != "audio/ogg")
            {
                throw new NotImplementedException();
            }

            var oggFilePath = string.Empty;
            var wavFilePath = string.Empty;

            try
            {
                oggFilePath = await DownloadOggFileAsync(audioFileUrl);

                wavFilePath = ConvertOggToWav(oggFilePath);

                var transcription = await TranscribeWavFile(wavFilePath, language);

                return transcription;
            }
            finally
            {
                File.Delete(oggFilePath);
                File.Delete(wavFilePath);
            }
        }

        private async Task<string> DownloadOggFileAsync(string url)
        {
            var fileBytes = await _httpClient.GetByteArrayAsync(url);

            var filePath = Path.GetTempFileName() + ".ogg";
            await File.WriteAllBytesAsync(filePath, fileBytes);

            return filePath;
        }

        private static string ConvertOggToWav(string sourceOggFile)
        {
            var wavOutputPath = Path.GetTempFileName() + ".wav";

            using var sourceFileStream = new FileStream(sourceOggFile, FileMode.Open);
            using var memoryStream = new MemoryStream();

            var decoder = new OpusDecoder(48000, 1);
            var oggReadStream = new OpusOggReadStream(decoder, sourceFileStream);

            while (oggReadStream.HasNextPacket)
            {
                short[] decodedPacket = oggReadStream.DecodeNextPacket();
                if (decodedPacket != null)
                {
                    foreach (var packet in decodedPacket)
                    {
                        var bytes = BitConverter.GetBytes(packet);
                        memoryStream.Write(bytes, 0, bytes.Length);
                    }
                }
            }

            memoryStream.Position = 0;
            var wavStream = new RawSourceWaveStream(memoryStream, new WaveFormat(48000, 16, 1));
            var sampleProvider = wavStream.ToSampleProvider();
            WaveFileWriter.CreateWaveFile16(wavOutputPath, sampleProvider);

            return wavOutputPath;
        }

        private async Task<string?> TranscribeWavFile(string wavFilePath, string language)
        {
            var config = SpeechConfig.FromSubscription(_subscriptionKey, _region);
            config.SpeechRecognitionLanguage = language;

            using var audioInput = AudioConfig.FromWavFileInput(wavFilePath);
            using var recognizer = new SpeechRecognizer(config, audioInput);

            var result = await recognizer.RecognizeOnceAsync();

            return result.Reason == ResultReason.RecognizedSpeech
                ? result.Text
                : null;
        }
    }
}
