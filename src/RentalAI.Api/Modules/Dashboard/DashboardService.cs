using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using RentalAI.Api.Data;
using RentalAI.Api.Modules.Booking;

namespace RentalAI.Api.Modules.Dashboard;

public sealed class DashboardService(AppDbContext db)
{
    public async Task<DashboardSummaryResponse> GetSummaryAsync(Guid ownerId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var fromAt = from.ToDateTime(TimeOnly.MinValue);
        var toAt = to.ToDateTime(TimeOnly.MinValue).AddDays(1);
        var rangeDays = Math.Max(1, to.DayNumber - from.DayNumber + 1);

        var properties = await db.Properties.AsNoTracking()
            .Where(p => p.OwnerId == ownerId)
            .Select(p => new { p.Id, p.Title })
            .ToListAsync(cancellationToken);

        var propertyIds = properties.Select(p => p.Id).ToList();

        var bookings = await db.Bookings.AsNoTracking()
            .Where(b => propertyIds.Contains(b.PropertyId)
                && (b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.Completed)
                && b.CheckIn < toAt && b.CheckOut > fromAt)
            .ToListAsync(cancellationToken);

        var totalRevenue = bookings.Sum(b => b.TotalPrice);
        var bookedNights = bookings.Sum(b => BookedNightsInRange(b.CheckIn, b.CheckOut, fromAt, toAt));
        var totalAvailableNights = properties.Count * rangeDays;
        var occupancy = totalAvailableNights == 0 ? 0m : Math.Round((decimal)bookedNights / totalAvailableNights, 4);

        var revenuePerProperty = properties
            .Select(p => new PropertyRevenue(p.Id, p.Title, bookings.Where(b => b.PropertyId == p.Id).Sum(b => b.TotalPrice)))
            .ToList();

        return new DashboardSummaryResponse(properties.Count, occupancy, totalRevenue, revenuePerProperty);
    }

    public async Task<byte[]> ExportAsync(Guid ownerId, Guid? propertyId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        var query = from booking in db.Bookings.AsNoTracking()
                    join property in db.Properties on booking.PropertyId equals property.Id
                    join guest in db.Users on booking.GuestId equals guest.Id
                    where property.OwnerId == ownerId
                        && (booking.Status == BookingStatus.Confirmed || booking.Status == BookingStatus.Completed)
                    select new
                    {
                        booking.CheckIn,
                        booking.CheckOut,
                        booking.TotalPrice,
                        GuestEmail = guest.Email,
                        PropertyTitle = property.Title,
                        booking.PropertyId
                    };

        if (propertyId is { } id)
        {
            query = query.Where(x => x.PropertyId == id);
        }

        if (from is { } fromDate)
        {
            var fromAt = fromDate.ToDateTime(TimeOnly.MinValue);
            query = query.Where(x => x.CheckIn >= fromAt);
        }

        if (to is { } toDate)
        {
            var toAt = toDate.ToDateTime(TimeOnly.MinValue).AddDays(1);
            query = query.Where(x => x.CheckIn < toAt);
        }

        var rows = await query.OrderBy(x => x.CheckIn).ToListAsync(cancellationToken);

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Bookings");
        sheet.Cell(1, 1).Value = "Rental Date";
        sheet.Cell(1, 2).Value = "Price Paid";
        sheet.Cell(1, 3).Value = "Guest Name";
        sheet.Cell(1, 4).Value = "Email";
        sheet.Cell(1, 5).Value = "Property";

        var rowNumber = 2;
        foreach (var row in rows)
        {
            sheet.Cell(rowNumber, 1).Value = row.CheckIn;
            sheet.Cell(rowNumber, 2).Value = row.TotalPrice;
            sheet.Cell(rowNumber, 3).Value = row.GuestEmail.Split('@')[0];
            sheet.Cell(rowNumber, 4).Value = row.GuestEmail;
            sheet.Cell(rowNumber, 5).Value = row.PropertyTitle;
            rowNumber++;
        }

        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static int BookedNightsInRange(DateTime checkIn, DateTime checkOut, DateTime fromAt, DateTime toAt)
    {
        var start = checkIn > fromAt ? checkIn : fromAt;
        var end = checkOut < toAt ? checkOut : toAt;
        var nights = (end.Date - start.Date).Days;
        return nights > 0 ? nights : 0;
    }
}
