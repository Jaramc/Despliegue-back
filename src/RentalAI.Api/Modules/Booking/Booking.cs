namespace RentalAI.Api.Modules.Booking;

public enum BookingStatus
{
    Pending,
    Confirmed,
    Cancelled,
    Completed
}

public sealed class Booking
{
    public Guid Id { get; set; }
    public Guid PropertyId { get; set; }
    public Guid GuestId { get; set; }
    public DateTime CheckIn { get; set; }
    public DateTime CheckOut { get; set; }
    public decimal TotalPrice { get; set; }
    public BookingStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
