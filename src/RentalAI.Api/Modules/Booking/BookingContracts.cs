namespace RentalAI.Api.Modules.Booking;

public sealed record CreateBookingRequest(Guid PropertyId, DateOnly CheckIn, DateOnly CheckOut);

public sealed record BookingResponse(
    Guid Id,
    Guid PropertyId,
    Guid GuestId,
    DateTime CheckIn,
    DateTime CheckOut,
    decimal TotalPrice,
    string Status,
    DateTime CreatedAt);

public enum BookingError
{
    None,
    PropertyNotFound,
    KycRequired,
    InvalidDates,
    Conflict,
    NotFound,
    Forbidden,
    InvalidState
}

public sealed record BookingOutcome(BookingError Error, BookingResponse? Booking)
{
    public static BookingOutcome Success(BookingResponse booking) => new(BookingError.None, booking);

    public static BookingOutcome Failed(BookingError error) => new(error, null);
}
