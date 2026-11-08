namespace CourseInquiry.Api.Tests.Services;

using CourseInquiry.Api.Data;
using CourseInquiry.Api.DTOs;
using CourseInquiry.Api.Models;
using CourseInquiry.Api.Services;
using Microsoft.EntityFrameworkCore;
using CourseInquiry.Api.Tests.Fakes;

public class InquiryServiceTests
{
    // Each test gets its own isolated in-memory database
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static CreateInquiryRequest ValidRequest() => new()
    {
        FirstName = "Jane",
        LastName = "Doe",
        Email = "jane@example.com",
        CourseName = "Intro to C#"
    };

    private static Inquiry SeedInquiry(AppDbContext db, string status, DateTime createdDate)
    {
        var inquiry = new Inquiry
        {
            FirstName = "Test",
            LastName = "User",
            Email = "test@example.com",
            CourseName = "Course",
            Status = status,
            CreatedDate = createdDate
        };
        db.Inquiries.Add(inquiry);
        db.SaveChanges();
        return inquiry;
    }

    // ---------- CreateInquiryAsync ----------

    [Fact]
    public async Task CreateInquiryAsync_NewInquiry_IsSavedWithStatusNew()
    {
        using var db = CreateDb();
        var service = new InquiryService(db, new FakeCrmSyncService());

        var result = await service.CreateInquiryAsync(ValidRequest());

        Assert.True(result.Id > 0);
        Assert.Equal("New", result.Status);
        Assert.Null(result.UpdatedDate);

        var saved = await db.Inquiries.SingleAsync();
        Assert.Equal(result.Id, saved.Id);
        Assert.Equal("New", saved.Status);
    }

    [Fact]
    public async Task CreateInquiryAsync_NormalisesInput()
    {
        using var db = CreateDb();
        var service = new InquiryService(db, new FakeCrmSyncService());
        var request = ValidRequest();
        request.FirstName = "  Jane  ";
        request.Email = "  Jane.Doe@Example.COM ";
        request.Phone = "   ";
        request.PreferredLocation = "";
        request.Message = "  Hello  ";

        var result = await service.CreateInquiryAsync(request);

        Assert.Equal("Jane", result.FirstName);
        Assert.Equal("jane.doe@example.com", result.Email);   // trimmed + lowercased
        Assert.Null(result.Phone);                            // whitespace -> null
        Assert.Null(result.PreferredLocation);                // empty -> null
        Assert.Equal("Hello", result.Message);                // trimmed
    }

    [Fact]
    public async Task CreateInquiryAsync_SetsCreatedDateInUtc()
    {
        using var db = CreateDb();
        var service = new InquiryService(db, new FakeCrmSyncService());
        var before = DateTime.UtcNow;

        var result = await service.CreateInquiryAsync(ValidRequest());

        Assert.Equal(DateTimeKind.Utc, result.CreatedDate.Kind);
        Assert.InRange(result.CreatedDate, before, DateTime.UtcNow);
    }

    // ---------- GetInquiriesAsync ----------

    [Fact]
    public async Task GetInquiriesAsync_WithStatus_ReturnsOnlyMatchingInquiries()
    {
        using var db = CreateDb();
        SeedInquiry(db, "New", DateTime.UtcNow);
        SeedInquiry(db, "Contacted", DateTime.UtcNow);
        SeedInquiry(db, "New", DateTime.UtcNow);
        var service = new InquiryService(db, new FakeCrmSyncService());

        var result = await service.GetInquiriesAsync(InquiryStatus.New);

        Assert.Equal(2, result.Count);
        Assert.All(result, i => Assert.Equal("New", i.Status));
    }

    [Fact]
    public async Task GetInquiriesAsync_NoStatus_ReturnsAllNewestFirst()
    {
        using var db = CreateDb();
        var oldest = SeedInquiry(db, "New", DateTime.UtcNow.AddDays(-2));
        var newest = SeedInquiry(db, "Closed", DateTime.UtcNow);
        var middle = SeedInquiry(db, "Pending", DateTime.UtcNow.AddDays(-1));
        var service = new InquiryService(db, new FakeCrmSyncService());

        var result = await service.GetInquiriesAsync();

        Assert.Equal(new[] { newest.Id, middle.Id, oldest.Id }, result.Select(i => i.Id));
    }

    // ---------- UpdateStatusAsync ----------

    [Fact]
    public async Task UpdateStatusAsync_ChangesStatusAndSetsUpdatedDate()
    {
        using var db = CreateDb();
        var inquiry = SeedInquiry(db, "New", DateTime.UtcNow.AddHours(-1));
        var service = new InquiryService(db, new FakeCrmSyncService());

        var result = await service.UpdateStatusAsync(inquiry.Id, InquiryStatus.Contacted);

        Assert.NotNull(result);
        Assert.Equal("Contacted", result.Status);
        Assert.NotNull(result.UpdatedDate);

        var saved = await db.Inquiries.AsNoTracking().SingleAsync(i => i.Id == inquiry.Id);
        Assert.Equal("Contacted", saved.Status);
    }

    [Fact]
    public async Task UpdateStatusAsync_SameStatus_DoesNotChangeUpdatedDate()
    {
        using var db = CreateDb();
        var inquiry = SeedInquiry(db, "New", DateTime.UtcNow);
        var service = new InquiryService(db, new FakeCrmSyncService());

        var result = await service.UpdateStatusAsync(inquiry.Id, InquiryStatus.New);

        Assert.NotNull(result);
        Assert.Equal("New", result.Status);
        Assert.Null(result.UpdatedDate);   // no-op update is not recorded as a change
    }

    [Fact]
    public async Task UpdateStatusAsync_UnknownId_ReturnsNull()
    {
        using var db = CreateDb();
        var service = new InquiryService(db, new FakeCrmSyncService());

        var result = await service.UpdateStatusAsync(999, InquiryStatus.Closed);

        Assert.Null(result);
    }

    // ---------- ArchiveInquiryAsync (soft delete) ----------

    [Fact]
    public async Task ArchiveInquiryAsync_SetsArchivedDate_AndKeepsRowInDatabase()
    {
        using var db = CreateDb();
        var inquiry = SeedInquiry(db, "Contacted", DateTime.UtcNow);
        var service = new InquiryService(db, new FakeCrmSyncService());

        var archived = await service.ArchiveInquiryAsync(inquiry.Id);

        Assert.True(archived);
        var row = await db.Inquiries.IgnoreQueryFilters().AsNoTracking().SingleAsync(i => i.Id == inquiry.Id);
        Assert.NotNull(row.ArchivedDate);           // soft-deleted, not removed
        Assert.Equal("Contacted", row.Status);      // original status preserved
    }

    [Fact]
    public async Task ArchivedInquiry_IsHiddenFromListGetAndUpdate()
    {
        using var db = CreateDb();
        var active = SeedInquiry(db, "New", DateTime.UtcNow);
        var archived = SeedInquiry(db, "New", DateTime.UtcNow);
        var service = new InquiryService(db, new FakeCrmSyncService());
        await service.ArchiveInquiryAsync(archived.Id);

        var list = await service.GetInquiriesAsync();
        var filtered = await service.GetInquiriesAsync(InquiryStatus.New);

        Assert.Equal(new[] { active.Id }, list.Select(i => i.Id));
        Assert.Equal(new[] { active.Id }, filtered.Select(i => i.Id));
        Assert.Null(await service.GetInquiryByIdAsync(archived.Id));
        Assert.Null(await service.UpdateStatusAsync(archived.Id, InquiryStatus.Closed));
    }

    [Fact]
    public async Task ArchiveInquiryAsync_AlreadyArchivedOrUnknown_ReturnsFalse()
    {
        using var db = CreateDb();
        var inquiry = SeedInquiry(db, "New", DateTime.UtcNow);
        var service = new InquiryService(db, new FakeCrmSyncService());
        await service.ArchiveInquiryAsync(inquiry.Id);

        Assert.False(await service.ArchiveInquiryAsync(inquiry.Id));   // second time
        Assert.False(await service.ArchiveInquiryAsync(999));          // never existed
    }

    // ---------- CRM hand-off ----------

    [Fact]
    public async Task CreateInquiryAsync_SendsSavedInquiryToCrm()
    {
        using var db = CreateDb();
        var crm = new FakeCrmSyncService();
        var service = new InquiryService(db, crm);

        var result = await service.CreateInquiryAsync(ValidRequest());

        Assert.Equal(new[] { result.Id }, crm.SyncedInquiryIds);   // synced once, after it had an Id
    }


}
