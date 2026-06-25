using FluentValidation;

namespace RentalAI.Api.Modules.Booking;

public sealed class CreateBookingRequestValidator : AbstractValidator<CreateBookingRequest>
{
    public CreateBookingRequestValidator()
    {
        RuleFor(x => x.PropertyId).NotEmpty();
        RuleFor(x => x.CheckOut).GreaterThan(x => x.CheckIn);
    }
}
