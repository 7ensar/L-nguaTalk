namespace LanguagePractice.Infrastructure.Services;

public class WebRtcOptions
{
    public const string SectionName = "WebRtc";

    /// <summary>JSON ICE server listesi (urls, username, credential).</summary>
    public List<IceServerOptions> IceServers { get; set; } =
    [
        new() { Urls = ["stun:stun.l.google.com:19302"] },
        new() { Urls = ["stun:stun1.l.google.com:19302"] }
    ];
}

public class IceServerOptions
{
    public List<string> Urls { get; set; } = [];
    public string? Username { get; set; }
    public string? Credential { get; set; }
}

public class AuthExternalOptions
{
    public const string SectionName = "Authentication";
    public GoogleAuthOptions Google { get; set; } = new();
}

public class GoogleAuthOptions
{
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public bool Enabled => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);
}
