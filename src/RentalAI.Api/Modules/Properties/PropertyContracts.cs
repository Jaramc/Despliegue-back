namespace RentalAI.Api.Modules.Properties;

public sealed record CreatePropertyRequest(
    string Title,
    string Description,
    string Address,
    string City,
    string Country,
    decimal Latitude,
    decimal Longitude,
    decimal NightlyRate,
    int MaxGuests,
    int Bedrooms,
    int Bathrooms);

public sealed record UpdatePropertyRequest(
    string Title,
    string Description,
    string Address,
    string City,
    string Country,
    decimal Latitude,
    decimal Longitude,
    decimal NightlyRate,
    int MaxGuests,
    int Bedrooms,
    int Bathrooms,
    bool IsActive);

public sealed record PropertySummaryResponse(
    Guid Id,
    Guid OwnerId,
    string Title,
    string City,
    string Country,
    decimal NightlyRate,
    int MaxGuests,
    string? MainPhotoUrl);

public sealed record PhotoResponse(Guid Id, string Url, int DisplayOrder);

public sealed record PropertyDetailResponse(
    Guid Id,
    Guid OwnerId,
    string Title,
    string Description,
    string Address,
    string City,
    string Country,
    decimal Latitude,
    decimal Longitude,
    decimal NightlyRate,
    int MaxGuests,
    int Bedrooms,
    int Bathrooms,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<PhotoResponse> Photos);

public sealed record PropertySearchResult(IReadOnlyList<PropertySummaryResponse> Items, int Page, int PageSize, int Total);

public sealed record PropertyQuery(
    Guid? OwnerId,
    string? City,
    string? Country,
    decimal? MinPrice,
    decimal? MaxPrice,
    int? Guests,
    DateOnly? CheckIn,
    DateOnly? CheckOut,
    int Page,
    int PageSize);

public enum PropertyMutationStatus
{
    Ok,
    NotFound,
    Forbidden,
    PhotoLimitReached
}

public sealed record PropertyMutationResult(PropertyMutationStatus Status, PhotoResponse? Photo = null, string? PhotoUrl = null);
