using Microsoft.EntityFrameworkCore;
using RentalAI.Api.Data;
using RentalAI.Api.Modules.Kyc;
using RentalAI.Api.Modules.Notifications;
using StackExchange.Redis;

namespace RentalAI.Api.Modules.Booking;

public sealed class BookingService(AppDbContext db, IConnectionMultiplexer redis, NotificationDispatcher notifications)
{
    private static readonly TimeSpan LockTtl = TimeSpan.FromSeconds(30);

    public async Task<BookingOutcome> CreateAsync(Guid guestId, CreateBookingRequest request, CancellationToken cancellationToken)
    {
        var kycApproved = await db.KycVerifications
            .AnyAsync(k => k.UserId == guestId && k.Verdict == KycVerdict.Approved, cancellationToken);
        if (!kycApproved)
        {
            return BookingOutcome.Failed(BookingError.KycRequired);
        }

        var nights = request.CheckOut.DayNumber - request.CheckIn.DayNumber;
        if (nights < 1)
        {
            return BookingOutcome.Failed(BookingError.InvalidDates);
        }

        var property = await db.Properties
            .SingleOrDefaultAsync(p => p.Id == request.PropertyId && p.IsActive, cancellationToken);
        if (property is null)
        {
            return BookingOutcome.Failed(BookingError.PropertyNotFound);
        }

        var checkIn = request.CheckIn.ToDateTime(TimeOnly.MinValue).AddHours(14);
        var checkOut = request.CheckOut.ToDateTime(TimeOnly.MinValue).AddHours(12);
        var totalPrice = property.NightlyRate * nights;

        var lockKey = $"lock:property:{request.PropertyId}:{request.CheckIn:yyyyMMdd}:{request.CheckOut:yyyyMMdd}";
        var lockToken = Guid.NewGuid().ToString();
        var database = redis.GetDatabase();

        if (!await database.StringSetAsync(lockKey, lockToken, LockTtl, When.NotExists))
        {
            return BookingOutcome.Failed(BookingError.Conflict);
        }

        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            await db.Properties
                .FromSql($"SELECT * FROM properties WHERE Id = {request.PropertyId} FOR UPDATE")
                .ToListAsync(cancellationToken);

            var overlaps = await db.Bookings.AnyAsync(b =>
                b.PropertyId == request.PropertyId
                && (b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.Pending)
                && b.CheckIn < checkOut
                && b.CheckOut > checkIn, cancellationToken);

            if (overlaps)
            {
                await transaction.RollbackAsync(cancellationToken);
                return BookingOutcome.Failed(BookingError.Conflict);
            }

            var now = DateTime.UtcNow;
            var booking = new Booking
            {
                Id = Guid.NewGuid(),
                PropertyId = request.PropertyId,
                GuestId = guestId,
                CheckIn = checkIn,
                CheckOut = checkOut,
                TotalPrice = totalPrice,
                Status = BookingStatus.Confirmed,
                CreatedAt = now,
                UpdatedAt = now
            };

            db.Bookings.Add(booking);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            var guestEmail = await db.Users
                .Where(u => u.Id == guestId)
                .Select(u => u.Email)
                .SingleAsync(cancellationToken);

            notifications.BookingConfirmed(guestId, guestEmail, property.OwnerId, property.Title, checkIn, checkOut, totalPrice);

            return BookingOutcome.Success(ToResponse(booking));
        }
        finally
        {
            await ReleaseLockAsync(database, lockKey, lockToken);
        }
    }

    public async Task<IReadOnlyList<BookingResponse>> GetMyBookingsAsync(Guid guestId, CancellationToken cancellationToken)
    {
        var bookings = await db.Bookings
            .AsNoTracking()
            .Where(b => b.GuestId == guestId)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(cancellationToken);

        return bookings.Select(ToResponse).ToList();
    }

    public async Task<BookingOutcome> GetByIdAsync(Guid userId, Guid bookingId, CancellationToken cancellationToken)
    {
        var booking = await db.Bookings.AsNoTracking().SingleOrDefaultAsync(b => b.Id == bookingId, cancellationToken);
        if (booking is null)
        {
            return BookingOutcome.Failed(BookingError.NotFound);
        }

        if (booking.GuestId != userId)
        {
            var ownsProperty = await db.Properties.AnyAsync(p => p.Id == booking.PropertyId && p.OwnerId == userId, cancellationToken);
            if (!ownsProperty)
            {
                return BookingOutcome.Failed(BookingError.Forbidden);
            }
        }

        return BookingOutcome.Success(ToResponse(booking));
    }

    public async Task<BookingOutcome> CancelAsync(Guid guestId, Guid bookingId, CancellationToken cancellationToken)
    {
        var booking = await db.Bookings.SingleOrDefaultAsync(b => b.Id == bookingId, cancellationToken);
        if (booking is null)
        {
            return BookingOutcome.Failed(BookingError.NotFound);
        }

        if (booking.GuestId != guestId)
        {
            return BookingOutcome.Failed(BookingError.Forbidden);
        }

        if (booking.Status is not (BookingStatus.Pending or BookingStatus.Confirmed))
        {
            return BookingOutcome.Failed(BookingError.InvalidState);
        }

        booking.Status = BookingStatus.Cancelled;
        booking.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return BookingOutcome.Success(ToResponse(booking));
    }

    private static async Task ReleaseLockAsync(IDatabase database, string key, string token)
    {
        var current = await database.StringGetAsync(key);
        if (current == token)
        {
            await database.KeyDeleteAsync(key);
        }
    }

    private static BookingResponse ToResponse(Booking booking) => new(
        booking.Id,
        booking.PropertyId,
        booking.GuestId,
        booking.CheckIn,
        booking.CheckOut,
        booking.TotalPrice,
        booking.Status.ToString(),
        booking.CreatedAt);
}
