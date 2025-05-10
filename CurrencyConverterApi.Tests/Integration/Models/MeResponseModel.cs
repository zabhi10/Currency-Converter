namespace CurrencyConverterApi.Tests.Integration.Models;

public class MeResponseModel
{
    public string? ClientId { get; set; }
    public IEnumerable<string>? Roles { get; set; }
    public bool IsAuthenticated { get; set; }
    public string? ExpiresAt { get; set; }
}
