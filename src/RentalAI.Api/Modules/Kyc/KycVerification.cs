using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RentalAI.Api.Modules.Kyc;

public enum KycVerdict
{
    Pending,
    Approved,
    Rejected
}

public sealed class KycVerification
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string DocumentType { get; set; } = null!;
    public string ExtractedData { get; set; } = null!;
    public KycVerdict Verdict { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class KycVerificationConfiguration : IEntityTypeConfiguration<KycVerification>
{
    public void Configure(EntityTypeBuilder<KycVerification> builder)
    {
        builder.ToTable("kyc_verifications");
        builder.HasKey(k => k.Id);
        builder.Property(k => k.DocumentType).HasMaxLength(64).IsRequired();
        builder.Property(k => k.ExtractedData).HasMaxLength(128).IsRequired();
        builder.Property(k => k.Verdict).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.HasIndex(k => k.UserId).IsUnique();
    }
}
