// Fully working, just use '''PERPLESITY_AI_API='random_api_key_in_single_quotes' dotnet run''' in terminal to run locally

using System.Text;
using System.Text.Json;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Identity.Web;
using SimpleBlazor.Components;
using SimpleBlazor.Voice;
using Twilio;

var builder = WebApplication.CreateBuilder(args);

builder
    .Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAdB2C"));

// Add services to the container.
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.Configure<ForwardingOptions>(builder.Configuration.GetSection("Forwarding"));
builder.Services.AddSingleton<CallForwardingService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
    // Only enforce HTTPS redirection in non-development environments.
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

// --- Twilio voice call forwarding ---
var twilioAccountSid =
    builder.Configuration["Twilio:AccountSid"]
    ?? Environment.GetEnvironmentVariable("TWILIO_ACCOUNT_SID");
var twilioAuthToken =
    builder.Configuration["Twilio:AuthToken"]
    ?? Environment.GetEnvironmentVariable("TWILIO_AUTH_TOKEN");

if (!string.IsNullOrWhiteSpace(twilioAccountSid) && !string.IsNullOrWhiteSpace(twilioAuthToken))
{
    TwilioClient.Init(twilioAccountSid, twilioAuthToken);
}
else
{
    Console.WriteLine(
        "Twilio credentials missing (Twilio:AccountSid / Twilio:AuthToken or env vars). Voice REST calls will be skipped."
    );
}

app.MapPost(
    "/voice/incoming",
    async (HttpRequest request, CallForwardingService forwarding) =>
    {
        var form = await request.ReadFormAsync();
        var inboundTwilioNumber = form["To"].ToString();

        var voiceResponse = forwarding.BuildForwardingResponse(inboundTwilioNumber);
        return Results.Content(voiceResponse.ToString(), "application/xml");
    }
);

// --- SMS chatbot backed by Perplexity ---
// Configuration: read from env vars or process config
var perplexityApiKey =
    builder.Configuration["PERPLEXITY_API_KEY"]
    ?? Environment.GetEnvironmentVariable("PERPLEXITY_API_KEY");

// Single HttpClient for the app
var http = new HttpClient
{
    Timeout = TimeSpan.FromSeconds(
        12
    ) // keep under Twilio 15s webhook timeout
    ,
};

// Twilio will POST form-encoded data with keys like "From" and "Body"
app.MapPost(
    "/sms",
    async (HttpRequest request) =>
    {
        var form = await request.ReadFormAsync();
        var from = form["From"].ToString();
        var body = form["Body"].ToString();

        Console.WriteLine($"Inbound SMS from {from}: {body}");

        string reply;
        if (string.IsNullOrWhiteSpace(body))
        {
            reply = "Hi! Send a question or try: WEATHER 94016";
        }
        else if (string.IsNullOrWhiteSpace(perplexityApiKey))
        {
            reply = "Setup needed: missing PERPLEXITY_API_KEY.";
        }
        else
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(11));
                reply = await GetPerplexityReplyAsync(http, perplexityApiKey!, body, cts.Token);
            }
            catch (TaskCanceledException)
            {
                reply = "I’m thinking a bit long—please try again.";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Perplexity error: {ex.Message}");
                reply = "Sorry, something went wrong. Try again.";
            }
        }

        // Keep replies SMS-friendly
        reply = TrimForSms(StripSourceCitations(reply), 300);
        var twiml = $"<Response><Message>{XmlEscape(reply)}</Message></Response>";
        return Results.Content(twiml, "application/xml");
    }
);

// Local functions
static string TrimForSms(string text, int max)
{
    if (string.IsNullOrEmpty(text))
        return text ?? string.Empty;
    text = text.Replace("\r\n", "\n").Replace("\r", "\n");
    if (text.Length <= max)
        return text;
    return text[..Math.Min(max, text.Length)].TrimEnd() + "…";
}

static string XmlEscape(string text)
{
    if (string.IsNullOrEmpty(text))
        return string.Empty;
    return text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}

static string StripSourceCitations(string text)
{
    if (string.IsNullOrWhiteSpace(text))
        return text ?? string.Empty;

    var withoutCitations = Regex.Replace(text, "\\[\\d+\\]", " ");

    return string.Join(
        " ",
        withoutCitations
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => segment.Trim())
    );
}

static async Task<string> GetPerplexityReplyAsync(
    HttpClient http,
    string apiKey,
    string userText,
    CancellationToken ct
)
{
    var endpoint = "https://api.perplexity.ai/chat/completions";

    var payload = new
    {
        model = "sonar-pro",
        temperature = 0.3,
        max_tokens = 240,
        messages = new object[]
        {
            new
            {
                role = "system",
                content = "You are a concise SMS assistant. Keep replies under 300 characters, plain text only.",
            },
            new { role = "user", content = userText },
        },
    };

    using var req = new HttpRequestMessage(HttpMethod.Post, endpoint)
    {
        Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "application/json"
        ),
    };
    req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
        "Bearer",
        apiKey
    );

    using var resp = await http.SendAsync(req, ct);
    resp.EnsureSuccessStatusCode();
    using var stream = await resp.Content.ReadAsStreamAsync(ct);

    using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
    var root = doc.RootElement;
    var content = root.GetProperty("choices")[0]
        .GetProperty("message")
        .GetProperty("content")
        .GetString();
    return content ?? "";
}

app.Run();
