using Microsoft.Extensions.Options;
using Twilio.TwiML;
using Twilio.TwiML.Voice;

namespace SimpleBlazor.Voice;

/// <summary>Builds TwiML instructions to forward inbound calls to configured destinations.</summary>
public sealed class CallForwardingService
{
    private readonly ForwardingOptions _options;

    public CallForwardingService(IOptions<ForwardingOptions> options) =>
        _options = options.Value;

    public VoiceResponse BuildForwardingResponse(string? inboundTwilioNumber)
    {
        var response = new VoiceResponse();

        if (string.IsNullOrWhiteSpace(inboundTwilioNumber))
        {
            response.Say("We could not identify the number you dialed. Please try again later.");
            return response;
        }

        var destination = ResolveDestination(inboundTwilioNumber);
        if (string.IsNullOrWhiteSpace(destination))
        {
            response.Say("This number is not configured yet. Goodbye.");
            return response;
        }

        // <Dial> forwards the call and preserves the caller ID the user dialed.
        var dial = new Dial(callerId: inboundTwilioNumber, timeout: 18, answerOnBridge: true);
        dial.Number(destination);

        response.Append(dial);
        return response;
    }

    private string? ResolveDestination(string inboundTwilioNumber)
    {
        if (_options.Routes.TryGetValue(inboundTwilioNumber, out var mapped))
        {
            return mapped;
        }

        return _options.PersonalNumber;
    }
}
