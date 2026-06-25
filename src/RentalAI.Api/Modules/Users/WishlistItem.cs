using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentalAI.Api.Modules.Properties;

namespace RentalAI.Api.Modules.Users;

public sealed class WishlistItem
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid PropertyId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class WishlistItemConfiguration : IEntityTypeConfiguration<WishlistItem>
{
    public void Configure(EntityTypeBuilder<WishlistItem> builder)
    {
        builder.ToTable("wishlist_items");
        builder.HasKey(w => w.Id);
        builder.HasIndex(w => new { w.UserId, w.PropertyId }).IsUnique();
        builder.HasOne<Property>().WithMany().HasForeignKey(w => w.PropertyId).OnDelete(DeleteBehavior.Cascade);
    }
}
