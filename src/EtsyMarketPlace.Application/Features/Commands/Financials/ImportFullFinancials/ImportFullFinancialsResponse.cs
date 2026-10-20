namespace EtsyMarketPlace.Application.Features.Commands.Financials.ImportFullFinancials;

public class ImportFullFinancialsResponse
{
    public bool Success { get; set; } = true;
    public string Message { get; set; } = string.Empty;
    public int TransactionsCount { get; set; }
    public int PayoutsCount { get; set; }
    public int AlertsCount { get; set; }
    public int MonthlyOrdersCount { get; set; }
    public int TrafficCount { get; set; }
    public int ChartsCount { get; set; }
}
