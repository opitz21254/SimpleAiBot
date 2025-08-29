namespace SimpleLogic;

using System;
using System.Threading.Tasks;
using Twilio;
using Twilio.AspNet.Core;
using Twilio.Types;
using Twilio.Rest.Api.V2010.Account;
using Newtonsoft.Json;
using System.Net.Http;

class Program
{
    static async Task Main(string[] args)
    {
        // Initialize Twilio
        string accountSid = "${{ secrets.TWILIO_SID }}";
        string authToken = "${{ secrets.TWILIO_AUTHTOKEN }}";
        TwilioClient.Init(accountSid, authToken);

        // Example: Receive and process an incoming SMS
        string userMessage = "This is a test. Write back with only \"TEST 1/1/2025\". Don't include anything else"; // Replace with input from Twilio webhook
        Console.WriteLine($"User Message: {userMessage}");

        // Call Perplexity API
        string aiResponse = await GetAIResponseAsync(userMessage);
        Console.WriteLine($"AI Response: {aiResponse}");

        // Send AI response back to the user via Twilio
        await SendSmsAsync("${{ secrets.MY_PHONENUMBER }}", aiResponse); // Replace with recipient's number
    }

    static async Task<string> GetAIResponseAsync(string userPrompt)
    {
        // Perplexity AI API Details
        string apiKey = "${{ secrets.PERPLEXITY_AI_API_KEY }}";
        string endpoint = "https://api.perplexity.ai/chat/completions";

        // Prepare HTTP Client
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

        // Construct payload
        var payload = new
        {
            model = "llama-3.1-sonar-large-128k-online",
            messages = new[]
            {
                new { role = "system", content = "You are an AI assistant." },
                new { role = "user", content = userPrompt }
            }
        };
        string jsonPayload = JsonConvert.SerializeObject(payload);

        // Send POST request
        var response = await httpClient.PostAsync(endpoint, new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json"));
        response.EnsureSuccessStatusCode();

        // Parse response
        string responseBody = await response.Content.ReadAsStringAsync();
        var aiResponse = JsonConvert.DeserializeObject<dynamic>(responseBody);
        return aiResponse?.choices[0]?.message?.content ?? "No response from Perplexity AI.";
    }

    static async Task SendSmsAsync(string to, string message)
    {
        // Send SMS using Twilio
        var messageResource = await MessageResource.CreateAsync(
            to: new PhoneNumber(to),
            from: new PhoneNumber("${{ secrets.TWILIO_PHONENUMBER }}"),
            body: message
        );
        Console.WriteLine($"Message SID: {messageResource.Sid}");
    }
}
