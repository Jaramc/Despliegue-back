namespace RentalAI.Api.Modules.Dashboard;

public sealed record PropertyRevenue(Guid PropertyId, string Title, decimal Revenue);

public sealed record DashboardSummaryResponse(
    int TotalProperties,
    decimal OccupancyRate,
    decimal TotalRevenue,
    IReadOnlyList<PropertyRevenue> RevenuePerProperty);
