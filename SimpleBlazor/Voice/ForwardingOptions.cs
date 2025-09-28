namespace SimpleBlazor.Voice;

/// <summary>Configuration binding for call forwarding destinations per Twilio number.</summary>
public sealed class ForwardingOptions
{
    public string? PersonalNumber { get; set; }

    public Dictionary<string, string> Routes { get; set; } = new(StringComparer.Ordinal);
}
