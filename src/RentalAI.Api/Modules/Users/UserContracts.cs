namespace RentalAI.Api.Modules.Users;

public sealed record ProfileResponse(
    Guid Id,
    string Email,
    string Role,
    string KycStatus,
    int BookingCount,
    DateTime CreatedAt);
