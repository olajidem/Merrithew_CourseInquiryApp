namespace CourseInquiry.Api.Data;

using CourseInquiry.Api.Models;
using Microsoft.EntityFrameworkCore;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<Inquiry> Inquiries => Set<Inquiry>();
    public DbSet<CrmSyncAttempt> CrmSyncAttempts => Set<CrmSyncAttempt>();


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Soft delete: archived inquiries are excluded from every query automatically.
        modelBuilder.Entity<Inquiry>().HasQueryFilter(i => i.ArchivedDate == null);
        modelBuilder.Entity<CrmSyncAttempt>(e =>
{
    // FK to Inquiries, no navigation property (avoids query-filter surprises with archived inquiries)
    e.HasOne<Inquiry>()
    .WithMany()
    .HasForeignKey(a => a.InquiryId)
    .OnDelete(DeleteBehavior.Cascade);

    e.HasIndex(a => a.InquiryId);
});

    }

}
