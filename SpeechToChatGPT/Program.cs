using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Newtonsoft.Json.Linq;

namespace SpeechToChatGPT
{
    internal class Program
    {
        static string speechKey = "";
        static string speechRegion = "";
        static string prompt = "";
        static string ChatGPTKey = "";
        static string WakeWord = "";
        static float temperature = (float)0.1;
        static string WakeWordFile = "";
        static string ChatGPTModel = "";
        static int ChatGPTMaxTokens;
        static string gotosleepphrase = "goodbye";
        static readonly HttpClient client = new HttpClient();

        static async Task Main(string[] args)
        {
            JObject config = JObject.Parse(File.ReadAllText("config.json"));
#pragma warning disable CS8602 // Dereference of a possibly null reference.
            if (config["debug"].ToObject<bool>())
            {
                Console.WriteLine("configfile: " + config.ToString());
            }

            speechKey = config["speechKey"].ToString();
            speechRegion = config["speechRegion"].ToString();
            prompt = config["prompt"].ToString();
            ChatGPTKey = config["ChatGPTKey"].ToString();
            temperature = config["ChatGPTtemperature"].ToObject<float>();
            WakeWord = config["WakeWord"].ToString();
            WakeWordFile = config["WakeWordFile"].ToString();
            ChatGPTModel = config["ChatGPTModel"].ToString();
            ChatGPTMaxTokens = config["ChatGPTMaxTokens"].ToObject<int>();
            bool debug = config["debug"].ToObject<bool>();
#pragma warning restore CS8602 // Dereference of a possibly null reference.

            var speechConfig = SpeechConfig.FromSubscription(speechKey, speechRegion);
            //https://learn.microsoft.com/en-us/azure/cognitive-services/speech-service/language-support?tabs=tts#prebuilt-neural-voices
            speechConfig.SpeechRecognitionLanguage = "en-US";
#pragma warning disable CS8602 // Dereference of a possibly null reference.
            speechConfig.SpeechSynthesisVoiceName = config["SpeechSynthesisVoiceName"].ToString();
#pragma warning restore CS8602 // Dereference of a possibly null reference.
            var keywordModel = KeywordRecognitionModel.FromFile(WakeWordFile);

            var audioConfig = AudioConfig.FromDefaultMicrophoneInput();
            var keywordRecognizer = new KeywordRecognizer(audioConfig);
            var speechRecognizer = new SpeechRecognizer(speechConfig, audioConfig);
            var speechSynthesizer = new SpeechSynthesizer(speechConfig);

            string initialPrompt = "Please wake me with " + WakeWord + ". to end the conversation say " + gotosleepphrase;
            await speechSynthesizer.SpeakTextAsync(initialPrompt);
            Console.WriteLine(initialPrompt);
            bool inConversation = false;

            while (true) {
                if (!inConversation)
                {
                    await keywordRecognizer.RecognizeOnceAsync(keywordModel);
                    await speechSynthesizer.SpeakTextAsync(prompt);
                }
                                
                var speechRecognitionResult = await speechRecognizer.RecognizeOnceAsync();
                if (debug)
                {
                    Console.WriteLine("Speech Results: " + speechRecognitionResult);
                }
                // response is speechRecognitionResult.Text
                Console.WriteLine(" I heard: " + speechRecognitionResult.Text);

                if (speechRecognitionResult.Text.ToLower().Contains(gotosleepphrase))
                {
                    string sleepprompt = "going to sleep.  you can wake me with " + WakeWord;
                    await speechSynthesizer.SpeakTextAsync(sleepprompt);
                    Console.WriteLine(" " + sleepprompt);
                    inConversation = false;
                    continue;
                } else
                {
                    inConversation = true;
                }

                //await speechSynthesizer.SpeakTextAsync("thinking");

                var values = new Dictionary<string, object>
                {
                    { "model", ChatGPTModel },
                    { "prompt", speechRecognitionResult.Text },
                    { "max_tokens", ChatGPTMaxTokens },
                    { "temperature", temperature }
                };
                var request = new HttpRequestMessage()
                {
                    Method = HttpMethod.Post,
                    RequestUri = new Uri("https://api.openai.com/v1/completions"),
                    Content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(values))
                };                               

                request.Headers.Add("Authorization", "Bearer " + ChatGPTKey);
                request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                
                DateTime startTime = DateTime.Now;
                if (debug)
                {
                    Console.WriteLine("Content: " + Newtonsoft.Json.JsonConvert.SerializeObject(values).ToString());
                    Console.WriteLine("Request: " + request.ToString());
                }

                var response = await client.SendAsync(request);
                JObject json = JObject.Parse(response.Content.ReadAsStringAsync().Result);
                
                if (debug)
                {
                    Console.WriteLine("Response from ChatGPT took " + (DateTime.Now - startTime).TotalMilliseconds + " milliseconds.");
                    Console.WriteLine("Response: " + json.ToString());
                }

#pragma warning disable CS8602 // Dereference of a possibly null reference.
                string ChatGPTAnswer = json["choices"][0]["text"].ToString().Trim();
#pragma warning restore CS8602 // Dereference of a possibly null reference.
                Console.WriteLine(" in " + (DateTime.Now - startTime).TotalMilliseconds + " MS, ChatGPT responded: " + ChatGPTAnswer);
                await speechSynthesizer.SpeakTextAsync(ChatGPTAnswer);
            }
        }
    }
}