namespace RentalAI.Api.Modules.Kyc;

public sealed record KycVerifyResponse(string Verdict, string? Reason);

public sealed record KycStatusResponse(string Verdict, DateTime? VerifiedAt);
