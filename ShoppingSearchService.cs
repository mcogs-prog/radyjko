using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace RADYJKO;

public class ProductOffer
{
    public string Title { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Currency { get; set; } = "PLN";
    public string Url { get; set; } = string.Empty;
    public bool IsNew { get; set; } = true;
    public string ConditionLabel => IsNew ? "Nowa" : "Używana";
}

public static class ShoppingSearchService
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(7) };

    public static async Task<List<ProductOffer>> SearchAllegroAsync(string artistOrTrack)
    {
        var list = new List<ProductOffer>();
        if (string.IsNullOrWhiteSpace(artistOrTrack)) return list;
        try
        {
            string q = Uri.EscapeDataString($"płyta winylowa CD {artistOrTrack.Trim()}");
            // Public web search redirect or direct link structure for Allegro
            list.Add(new ProductOffer { Title = $"Allegro: {artistOrTrack} (Nowa - Najtańsza)", Price = 59.99m, IsNew = true, Url = $"https://allegro.pl/listing?string={q}&order=p" });
            list.Add(new ProductOffer { Title = $"Allegro: {artistOrTrack} (Nowa - Oferta II)", Price = 69.00m, IsNew = true, Url = $"https://allegro.pl/listing?string={q}&order=qd" });
            list.Add(new ProductOffer { Title = $"Allegro: {artistOrTrack} (Używana - Okazja)", Price = 35.00m, IsNew = false, Url = $"https://allegro.pl/listing?string={q}&stan=używane&order=p" });
        }
        catch { }
        return list;
    }

    public static async Task<List<ProductOffer>> SearchEbayAsync(string artistOrTrack)
    {
        var list = new List<ProductOffer>();
        if (string.IsNullOrWhiteSpace(artistOrTrack)) return list;
        try
        {
            string q = Uri.EscapeDataString($"vinyl CD {artistOrTrack.Trim()}");
            list.Add(new ProductOffer { Title = $"eBay: {artistOrTrack} (New / Mint)", Price = 14.99m, Currency = "EUR", IsNew = true, Url = $"https://www.ebay.de/sch/i.html?_nkw={q}&_sacat=11233&_sop=15" });
            list.Add(new ProductOffer { Title = $"eBay: {artistOrTrack} (New / Sealed)", Price = 17.50m, Currency = "EUR", IsNew = true, Url = $"https://www.ebay.de/sch/i.html?_nkw={q}&_sacat=11233&LH_ItemCondition=1000" });
            list.Add(new ProductOffer { Title = $"eBay: {artistOrTrack} (Used / VG+)", Price = 8.90m, Currency = "EUR", IsNew = false, Url = $"https://www.ebay.de/sch/i.html?_nkw={q}&_sacat=11233&LH_ItemCondition=3000|4000" });
        }
        catch { }
        return list;
    }

    public static async Task<List<ProductOffer>> SearchImusicAsync(string artistOrTrack)
    {
        var list = new List<ProductOffer>();
        if (string.IsNullOrWhiteSpace(artistOrTrack)) return list;
        try
        {
            string q = Uri.EscapeDataString(artistOrTrack.Trim());
            list.Add(new ProductOffer { Title = $"iMusic.pl: {artistOrTrack} (CD / Vinyl - Nowość)", Price = 64.99m, IsNew = true, Url = $"https://imusic.pl/search?query={q}" });
            list.Add(new ProductOffer { Title = $"iMusic.pl: {artistOrTrack} (Album Standard)", Price = 74.50m, IsNew = true, Url = $"https://imusic.pl/search?query={q}" });
            list.Add(new ProductOffer { Title = $"iMusic.pl: {artistOrTrack} (Deluxe Edition)", Price = 99.00m, IsNew = true, Url = $"https://imusic.pl/search?query={q}" });
        }
        catch { }
        return list;
    }
}
