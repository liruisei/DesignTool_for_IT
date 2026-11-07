namespace WinDesign.Api.Services;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "WinDesign";
    public string Audience { get; set; } = "WinDesign.Client";
    public string Key { get; set; } = string.Empty;
    public int ExpiresHours { get; set; } = 8;
}
