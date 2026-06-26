using Microsoft.EntityFrameworkCore;
using RentalAI.Api.Data;
using RentalAI.Api.Modules.Kyc;
using RentalAI.Api.Modules.Properties;

namespace RentalAI.Api.Modules.Users;

public sealed class WishlistService(AppDbContext db)
{
    public async Task<bool> AddAsync(Guid userId, Guid propertyId, CancellationToken cancellationToken)
    {
        var propertyExists = await db.Properties.AnyAsync(p => p.Id == propertyId, cancellationToken);
        if (!propertyExists)
        {
            return false;
        }

        var alreadyAdded = await db.WishlistItems.AnyAsync(w => w.UserId == userId && w.PropertyId == propertyId, cancellationToken);
        if (!alreadyAdded)
        {
            db.WishlistItems.Add(new WishlistItem
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                PropertyId = propertyId,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    public async Task RemoveAsync(Guid userId, Guid propertyId, CancellationToken cancellationToken)
    {
        var item = await db.WishlistItems.SingleOrDefaultAsync(w => w.UserId == userId && w.PropertyId == propertyId, cancellationToken);
        if (item is not null)
        {
            db.WishlistItems.Remove(item);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IReadOnlyList<PropertySummaryResponse>> GetForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var propertyIds = db.WishlistItems.Where(w => w.UserId == userId).Select(w => w.PropertyId);
        return await SummariesAsync(propertyIds, cancellationToken);
    }

    public async Task<IReadOnlyList<PropertySummaryResponse>> GetSummariesAsync(IEnumerable<Guid> propertyIds, CancellationToken cancellationToken)
    {
        var ids = propertyIds.ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        return await SummariesAsync(db.Properties.Where(p => ids.Contains(p.Id)).Select(p => p.Id), cancellationToken);
    }

    public async Task MergeAsync(Guid userId, IReadOnlyList<Guid> propertyIds, CancellationToken cancellationToken)
    {
        if (propertyIds.Count == 0)
        {
            return;
        }

        var validIds = await db.Properties
            .Where(p => propertyIds.Contains(p.Id))
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        var existing = await db.WishlistItems
            .Where(w => w.UserId == userId && validIds.Contains(w.PropertyId))
            .Select(w => w.PropertyId)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        foreach (var propertyId in validIds.Except(existing))
        {
            db.WishlistItems.Add(new WishlistItem
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                PropertyId = propertyId,
                CreatedAt = now
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<ProfileResponse?> GetProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var kyc = await db.KycVerifications.AsNoTracking().SingleOrDefaultAsync(k => k.UserId == userId, cancellationToken);
        var bookingCount = await db.Bookings.CountAsync(b => b.GuestId == userId, cancellationToken);

        return new ProfileResponse(
            user.Id,
            user.Email,
            user.Role.ToString(),
            (kyc?.Verdict ?? KycVerdict.Pending).ToString(),
            bookingCount,
            user.CreatedAt);
    }

    private async Task<IReadOnlyList<PropertySummaryResponse>> SummariesAsync(IQueryable<Guid> propertyIds, CancellationToken cancellationToken) =>
        await db.Properties
            .AsNoTracking()
            .Where(p => propertyIds.Contains(p.Id) && p.IsActive)
            .Select(p => new PropertySummaryResponse(
                p.Id,
                p.OwnerId,
                p.Title,
                p.City,
                p.Country,
                p.NightlyRate,
                p.MaxGuests,
                p.Photos.OrderBy(photo => photo.DisplayOrder).Select(photo => photo.Url).FirstOrDefault()))
            .ToListAsync(cancellationToken);
}
