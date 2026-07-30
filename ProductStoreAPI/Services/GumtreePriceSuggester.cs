using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using ProductStoreAPI.Contracts;

namespace ProductStoreAPI.Services;

// Prices second-hand items from Gumtree South Africa (ZAR). Gumtree has no search API,
// so we fetch its server-rendered results page and read each listing's data- attributes.
// Every listing is one <div class="watchListSRP"> carrying title, link and price together.
public partial class GumtreePriceSuggester : IPriceSuggester
{
    private readonly HttpClient _http;

    public GumtreePriceSuggester(HttpClient http)
    {
        _http = http;
    }

    public async Task<PriceSuggestion> SuggestPriceAsync(string query, string? category, CancellationToken ct)
    {
        // Gumtree uses '+' for spaces in the search slug, e.g. /s-logitech+mouse/v1q0p1.
        var slug = Uri.EscapeDataString(query.Trim()).Replace("%20", "+");
        var html = await _http.GetStringAsync($"/s-{slug}/v1q0p1", ct);

        var comparables = new List<ComparableListing>();
        foreach (Match card in CardRegex().Matches(html))
        {
            var attrs = card.Groups["attrs"].Value;
            var title = Attr(attrs, "data-adTitle");
            var link = Attr(attrs, "data-adLink");
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(link)) continue;

            var url = new Uri(_http.BaseAddress!, link).ToString();
            comparables.Add(new ComparableListing(
                WebUtility.HtmlDecode(title),
                ParsePrice(Attr(attrs, "data-adPrice")),
                url));
        }

        return new PriceSuggestion(Median(comparables), comparables);
    }

    // Median resists the odd off-topic outlier (a gaming PC in a "mouse" search) without
    // needing a model to judge relevance.
    private static decimal? Median(IReadOnlyList<ComparableListing> comparables)
    {
        var prices = comparables
            .Where(c => c.Price is > 0)
            .Select(c => c.Price!.Value)
            .OrderBy(p => p)
            .ToList();

        if (prices.Count == 0) return null;

        var mid = prices.Count / 2;
        return prices.Count % 2 == 1
            ? prices[mid]
            : (prices[mid - 1] + prices[mid]) / 2m;
    }

    private static decimal? ParsePrice(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        // "R 999", "R1199", "R 1,250" -> 999 / 1199 / 1250
        var digits = new string(raw.Where(char.IsDigit).ToArray());
        return decimal.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    private static string Attr(string attrs, string name)
    {
        var m = Regex.Match(attrs, $"{name}=\"([^\"]*)\"");
        return m.Success ? m.Groups[1].Value : "";
    }

    [GeneratedRegex("<div class=\"watchListSRP\"(?<attrs>[^>]*)>")]
    private static partial Regex CardRegex();
}
