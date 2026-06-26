using Microsoft.EntityFrameworkCore;
using RentalAI.Api.Data;
using RentalAI.Api.Modules.Booking;

namespace RentalAI.Api.Modules.Properties;

public sealed class PropertyService(AppDbContext db)
{
    private const int MaxPhotosPerProperty = 10;

    public async Task<PropertySearchResult> SearchAsync(PropertyQuery query, CancellationToken cancellationToken)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 100 ? 20 : query.PageSize;

        var properties = db.Properties.AsNoTracking().Where(p => p.IsActive);

        if (query.OwnerId is { } ownerId)
        {
            properties = properties.Where(p => p.OwnerId == ownerId);
        }

        if (!string.IsNullOrWhiteSpace(query.City))
        {
            properties = properties.Where(p => p.City == query.City);
        }

        if (!string.IsNullOrWhiteSpace(query.Country))
        {
            properties = properties.Where(p => p.Country == query.Country);
        }

        if (query.MinPrice is { } minPrice)
        {
            properties = properties.Where(p => p.NightlyRate >= minPrice);
        }

        if (query.MaxPrice is { } maxPrice)
        {
            properties = properties.Where(p => p.NightlyRate <= maxPrice);
        }

        if (query.Guests is { } guests)
        {
            properties = properties.Where(p => p.MaxGuests >= guests);
        }

        if (query.CheckIn is { } checkIn && query.CheckOut is { } checkOut)
        {
            var checkInAt = checkIn.ToDateTime(TimeOnly.MinValue).AddHours(14);
            var checkOutAt = checkOut.ToDateTime(TimeOnly.MinValue).AddHours(12);
            var bookedPropertyIds = db.Bookings
                .Where(b => (b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.Pending)
                    && b.CheckIn < checkOutAt && b.CheckOut > checkInAt)
                .Select(b => b.PropertyId);
            properties = properties.Where(p => !bookedPropertyIds.Contains(p.Id));
        }

        var total = await properties.CountAsync(cancellationToken);

        var items = await properties
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
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

        return new PropertySearchResult(items, page, pageSize, total);
    }

    public async Task<PropertyDetailResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var property = await db.Properties
            .AsNoTracking()
            .Include(p => p.Photos)
            .SingleOrDefaultAsync(p => p.Id == id, cancellationToken);

        return property is null ? null : ToDetail(property);
    }

    public async Task<Guid> CreateAsync(Guid ownerId, CreatePropertyRequest request, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var property = new Property
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            Title = request.Title,
            Description = request.Description,
            Address = request.Address,
            City = request.City,
            Country = request.Country,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            NightlyRate = request.NightlyRate,
            MaxGuests = request.MaxGuests,
            Bedrooms = request.Bedrooms,
            Bathrooms = request.Bathrooms,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Properties.Add(property);
        await db.SaveChangesAsync(cancellationToken);
        return property.Id;
    }

    public async Task<PropertyMutationResult> UpdateAsync(Guid ownerId, Guid id, UpdatePropertyRequest request, CancellationToken cancellationToken)
    {
        var property = await db.Properties.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (property is null)
        {
            return new PropertyMutationResult(PropertyMutationStatus.NotFound);
        }

        if (property.OwnerId != ownerId)
        {
            return new PropertyMutationResult(PropertyMutationStatus.Forbidden);
        }

        property.Title = request.Title;
        property.Description = request.Description;
        property.Address = request.Address;
        property.City = request.City;
        property.Country = request.Country;
        property.Latitude = request.Latitude;
        property.Longitude = request.Longitude;
        property.NightlyRate = request.NightlyRate;
        property.MaxGuests = request.MaxGuests;
        property.Bedrooms = request.Bedrooms;
        property.Bathrooms = request.Bathrooms;
        property.IsActive = request.IsActive;
        property.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return new PropertyMutationResult(PropertyMutationStatus.Ok);
    }

    public async Task<PropertyMutationResult> SoftDeleteAsync(Guid ownerId, Guid id, CancellationToken cancellationToken)
    {
        var property = await db.Properties.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (property is null)
        {
            return new PropertyMutationResult(PropertyMutationStatus.NotFound);
        }

        if (property.OwnerId != ownerId)
        {
            return new PropertyMutationResult(PropertyMutationStatus.Forbidden);
        }

        property.IsActive = false;
        property.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return new PropertyMutationResult(PropertyMutationStatus.Ok);
    }

    public async Task<(PropertyMutationStatus Status, int PhotoCount)> CheckPhotoSlotAsync(Guid ownerId, Guid propertyId, CancellationToken cancellationToken)
    {
        var property = await db.Properties
            .Select(p => new { p.Id, p.OwnerId, PhotoCount = p.Photos.Count })
            .SingleOrDefaultAsync(p => p.Id == propertyId, cancellationToken);

        if (property is null)
        {
            return (PropertyMutationStatus.NotFound, 0);
        }

        if (property.OwnerId != ownerId)
        {
            return (PropertyMutationStatus.Forbidden, 0);
        }

        return property.PhotoCount >= MaxPhotosPerProperty
            ? (PropertyMutationStatus.PhotoLimitReached, property.PhotoCount)
            : (PropertyMutationStatus.Ok, property.PhotoCount);
    }

    public async Task<PhotoResponse> AddPhotoAsync(Guid propertyId, string url, int displayOrder, CancellationToken cancellationToken)
    {
        var photo = new PropertyPhoto
        {
            Id = Guid.NewGuid(),
            PropertyId = propertyId,
            Url = url,
            DisplayOrder = displayOrder,
            CreatedAt = DateTime.UtcNow
        };

        db.PropertyPhotos.Add(photo);
        await db.SaveChangesAsync(cancellationToken);
        return new PhotoResponse(photo.Id, photo.Url, photo.DisplayOrder);
    }

    public async Task<PropertyMutationResult> DeletePhotoAsync(Guid ownerId, Guid propertyId, Guid photoId, CancellationToken cancellationToken)
    {
        var property = await db.Properties.SingleOrDefaultAsync(p => p.Id == propertyId, cancellationToken);
        if (property is null)
        {
            return new PropertyMutationResult(PropertyMutationStatus.NotFound);
        }

        if (property.OwnerId != ownerId)
        {
            return new PropertyMutationResult(PropertyMutationStatus.Forbidden);
        }

        var photo = await db.PropertyPhotos.SingleOrDefaultAsync(p => p.Id == photoId && p.PropertyId == propertyId, cancellationToken);
        if (photo is null)
        {
            return new PropertyMutationResult(PropertyMutationStatus.NotFound);
        }

        db.PropertyPhotos.Remove(photo);
        await db.SaveChangesAsync(cancellationToken);
        return new PropertyMutationResult(PropertyMutationStatus.Ok, PhotoUrl: photo.Url);
    }

    private static PropertyDetailResponse ToDetail(Property property) => new(
        property.Id,
        property.OwnerId,
        property.Title,
        property.Description,
        property.Address,
        property.City,
        property.Country,
        property.Latitude,
        property.Longitude,
        property.NightlyRate,
        property.MaxGuests,
        property.Bedrooms,
        property.Bathrooms,
        property.IsActive,
        property.CreatedAt,
        property.UpdatedAt,
        property.Photos
            .OrderBy(photo => photo.DisplayOrder)
            .Select(photo => new PhotoResponse(photo.Id, photo.Url, photo.DisplayOrder))
            .ToList());
}
