namespace SimilarProductsWinForms.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using EtsyMarketPlace.Application.ShopPerformance;

internal static class ShopParetoAnalysisService
{
    public enum ParetoCategory
    {
        A_Stars,        // İlk %80 Ciro (Lokomotif Ürünler)
        B_Potentials,   // Sonraki %15 Ciro (Potansiyelli Ürünler)
        C_SlowMovers    // Kalan %5 Ciro veya 0 Satış (Yavaş Satan / Ölü Stok)
    }

    public sealed class ParetoItem
    {
        public long ListingId { get; set; }
        public string Title { get; set; } = string.Empty;
        public int UnitsSold { get; set; }
        public int OrdersCount { get; set; }
        public decimal Revenue { get; set; }
        public decimal RevenueSharePercent { get; set; }
        public decimal CumulativeSharePercent { get; set; }
        public ParetoCategory Category { get; set; }
        public string CategoryDisplay => Category switch
        {
            ParetoCategory.A_Stars => "🟢 A (Yıldız Ürün - %80 Ciro)",
            ParetoCategory.B_Potentials => "🟡 B (Potansiyelli Ürün)",
            _ => "🔴 C (Yavaş Satan / İyileştirilmeli)"
        };
    }

    public sealed class ParetoSummary
    {
        public List<ParetoItem> Items { get; set; } = [];
        public int GroupACount { get; set; }
        public int GroupBCount { get; set; }
        public int GroupCCount { get; set; }
        public decimal GroupARevenue { get; set; }
        public decimal GroupBRevenue { get; set; }
        public decimal GroupCRevenue { get; set; }
        public decimal TotalRevenue { get; set; }
    }

    public static ParetoSummary Analyze(IReadOnlyList<ProductPerformance> products)
    {
        var summary = new ParetoSummary();
        if (products == null || products.Count == 0) return summary;

        var sorted = products.OrderByDescending(p => p.Revenue).ToList();
        summary.TotalRevenue = sorted.Sum(p => p.Revenue);

        decimal runningRevenue = 0;

        foreach (var p in sorted)
        {
            runningRevenue += p.Revenue;
            decimal share = summary.TotalRevenue > 0 ? (p.Revenue / summary.TotalRevenue) * 100m : 0;
            decimal cumulative = summary.TotalRevenue > 0 ? (runningRevenue / summary.TotalRevenue) * 100m : 100;

            ParetoCategory cat;
            if (cumulative <= 80m || (sorted.IndexOf(p) == 0 && share > 50m))
            {
                cat = ParetoCategory.A_Stars;
                summary.GroupACount++;
                summary.GroupARevenue += p.Revenue;
            }
            else if (cumulative <= 95m)
            {
                cat = ParetoCategory.B_Potentials;
                summary.GroupBCount++;
                summary.GroupBRevenue += p.Revenue;
            }
            else
            {
                cat = ParetoCategory.C_SlowMovers;
                summary.GroupCCount++;
                summary.GroupCRevenue += p.Revenue;
            }

            summary.Items.Add(new ParetoItem
            {
                ListingId = p.ListingId,
                Title = p.Title,
                UnitsSold = p.UnitsSold,
                OrdersCount = p.OrderCount,
                Revenue = p.Revenue,
                RevenueSharePercent = Math.Round(share, 1),
                CumulativeSharePercent = Math.Round(cumulative, 1),
                Category = cat
            });
        }

        return summary;
    }
}
