using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace TicketFlow.Infrastructure.Search;

public sealed record SearchSettings(bool Enabled, Uri Endpoint, string Index, int IntervalSeconds)
{
    public static SearchSettings Load(IConfiguration configuration)
    {
        var enabled = configuration.GetValue<bool>("Search:Enabled");
        var endpoint = configuration["Search:Endpoint"] ?? "http://localhost:9200";
        var index = configuration["Search:Index"] ?? "ticketflow-public-v1";
        var interval = configuration.GetValue("Search:IntervalSeconds", 15);
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")
            || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment)
            || !Regex.IsMatch(index, "^[a-z0-9][a-z0-9_-]{0,99}$") || interval is < 1 or > 3600)
            throw new InvalidOperationException("Configure a valid Search endpoint, lowercase index name, and interval of 1 to 3600 seconds.");
        return new(enabled, uri, index, interval);
    }
}
