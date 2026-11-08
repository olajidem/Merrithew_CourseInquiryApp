namespace CourseInquiry.Api.Tests.Controllers;

using CourseInquiry.Api.Controllers;
using CourseInquiry.Api.DTOs;
using CourseInquiry.Api.Models;
using CourseInquiry.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using CourseInquiry.Api.Tests.Fakes;
using CourseInquiry.Api.Integrations.Crm;


public class InquiriesControllerTests
{
    // Hand-written fake: returns what the test sets up and records what it was called with
    private sealed class FakeInquiryService : IInquiryService
    {
        public InquiryResponse? InquiryToReturn { get; set; }
        public InquiryStatus? LastStatusFilter { get; private set; }
        public bool UpdateCalled { get; private set; }
        public bool ArchiveResult { get; set; }

        public Task<bool> ArchiveInquiryAsync(int id, CancellationToken ct = default) =>
            Task.FromResult(ArchiveResult);


        public Task<InquiryResponse> CreateInquiryAsync(CreateInquiryRequest request, CancellationToken ct = default) =>
            Task.FromResult(InquiryToReturn!);

        public Task<IReadOnlyList<InquiryResponse>> GetInquiriesAsync(InquiryStatus? status = null, CancellationToken ct = default)
        {
            LastStatusFilter = status;
            return Task.FromResult<IReadOnlyList<InquiryResponse>>(new List<InquiryResponse>());
        }

        public Task<InquiryResponse?> GetInquiryByIdAsync(int id, CancellationToken ct = default) =>
            Task.FromResult(InquiryToReturn);

        public Task<InquiryResponse?> UpdateStatusAsync(int id, InquiryStatus status, CancellationToken ct = default)
        {
            UpdateCalled = true;
            return Task.FromResult(InquiryToReturn);
        }
    }

    private static InquiriesController CreateController(FakeInquiryService service, FakeCrmSyncService? crm = null)
    {
        // Problem() and ValidationProblem() need MVC services from the HttpContext
        var services = new ServiceCollection().AddLogging().AddMvcCore().Services.BuildServiceProvider();
        return new InquiriesController(service, crm ?? new FakeCrmSyncService())

        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { RequestServices = services }
            }
        };
    }

    private static InquiryResponse SampleInquiry(int id = 1, string status = "New") => new()
    {
        Id = id,
        FirstName = "Jane",
        LastName = "Doe",
        Email = "jane@example.com",
        CourseName = "Intro to C#",
        Status = status,
        CreatedDate = DateTime.UtcNow
    };

    // ---------- POST ----------

    [Fact]
    public async Task Create_Returns201WithLocationOfNewInquiry()
    {
        var service = new FakeInquiryService { InquiryToReturn = SampleInquiry(id: 42) };
        var controller = CreateController(service);

        var result = await controller.Create(new CreateInquiryRequest(), CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        Assert.Equal(nameof(InquiriesController.GetById), created.ActionName);
        Assert.Equal(42, created.RouteValues!["id"]);
        Assert.Same(service.InquiryToReturn, created.Value);
    }

    // ---------- GET list ----------

    [Theory]
    [InlineData("New", InquiryStatus.New)]
    [InlineData("contacted", InquiryStatus.Contacted)]   // case-insensitive
    [InlineData("REGISTERED", InquiryStatus.Registered)]
    public async Task GetAll_ValidStatus_PassesParsedStatusToService(string query, InquiryStatus expected)
    {
        var service = new FakeInquiryService();
        var controller = CreateController(service);

        var result = await controller.GetAll(query, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(expected, service.LastStatusFilter);
    }

    [Fact]
    public async Task GetAll_NoStatus_ReturnsAll()
    {
        var service = new FakeInquiryService();
        var controller = CreateController(service);

        var result = await controller.GetAll(null, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result.Result);
        Assert.Null(service.LastStatusFilter);
    }

    [Theory]
    [InlineData("foo")]
    [InlineData("99")]   // numeric value that isn't a defined enum member
    public async Task GetAll_InvalidStatus_Returns400(string query)
    {
        var controller = CreateController(new FakeInquiryService());

        var result = await controller.GetAll(query, CancellationToken.None);

        var problem = Assert.IsAssignableFrom<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        var details = Assert.IsType<ValidationProblemDetails>(problem.Value);
        Assert.Contains("status", details.Errors.Keys);
    }

    // ---------- GET by id ----------

    [Fact]
    public async Task GetById_Exists_Returns200()
    {
        var controller = CreateController(new FakeInquiryService { InquiryToReturn = SampleInquiry(id: 7) });

        var result = await controller.GetById(7, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal(7, Assert.IsType<InquiryResponse>(ok.Value).Id);
    }

    [Fact]
    public async Task GetById_Missing_Returns404WithDetail()
    {
        var controller = CreateController(new FakeInquiryService { InquiryToReturn = null });

        var result = await controller.GetById(999, CancellationToken.None);

        var problem = Assert.IsAssignableFrom<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status404NotFound, problem.StatusCode);
        Assert.Contains("999", Assert.IsType<ProblemDetails>(problem.Value).Detail);
    }

    // ---------- PATCH status ----------

    [Fact]
    public async Task UpdateStatus_ValidStatus_Returns200WithUpdatedInquiry()
    {
        var service = new FakeInquiryService { InquiryToReturn = SampleInquiry(status: "Contacted") };
        var controller = CreateController(service);

        var result = await controller.UpdateStatus(1, new UpdateInquiryStatusRequest { Status = "Contacted" }, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal("Contacted", Assert.IsType<InquiryResponse>(ok.Value).Status);
    }

    [Fact]
    public async Task UpdateStatus_InvalidStatus_Returns400AndDoesNotCallService()
    {
        var service = new FakeInquiryService { InquiryToReturn = SampleInquiry() };
        var controller = CreateController(service);

        var result = await controller.UpdateStatus(1, new UpdateInquiryStatusRequest { Status = "Admin" }, CancellationToken.None);

        var problem = Assert.IsAssignableFrom<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.False(service.UpdateCalled);   // invalid input never reaches the service
    }

    [Fact]
    public async Task UpdateStatus_MissingInquiry_Returns404()
    {
        var controller = CreateController(new FakeInquiryService { InquiryToReturn = null });

        var result = await controller.UpdateStatus(999, new UpdateInquiryStatusRequest { Status = "Closed" }, CancellationToken.None);

        var problem = Assert.IsAssignableFrom<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status404NotFound, problem.StatusCode);
    }

    // ---------- DELETE (archive) ----------

    [Fact]
    public async Task Archive_Exists_Returns204()
    {
        var controller = CreateController(new FakeInquiryService { ArchiveResult = true });

        var result = await controller.Archive(1, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Archive_MissingOrAlreadyArchived_Returns404()
    {
        var controller = CreateController(new FakeInquiryService { ArchiveResult = false });

        var result = await controller.Archive(999, CancellationToken.None);

        var problem = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, problem.StatusCode);
    }

    // ---------- CRM sync endpoints ----------

    [Fact]
    public async Task RetryCrmSync_Success_Returns200()
    {
        var crm = new FakeCrmSyncService { RetryResult = CrmSyncResult.Succeeded("CRM-ABC12345") };
        var controller = CreateController(new FakeInquiryService(), crm);

        var result = await controller.RetryCrmSync(1, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task RetryCrmSync_Failure_Returns502WithReason()
    {
        var crm = new FakeCrmSyncService { RetryResult = CrmSyncResult.Failed("CRM unavailable (HttpRequestException).", isTransient: true) };
        var controller = CreateController(new FakeInquiryService(), crm);

        var result = await controller.RetryCrmSync(1, CancellationToken.None);

        var problem = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status502BadGateway, problem.StatusCode);
        Assert.Equal("CRM unavailable (HttpRequestException).", Assert.IsType<ProblemDetails>(problem.Value).Detail);
    }

    [Fact]
    public async Task RetryCrmSync_MissingInquiry_Returns404()
    {
        var controller = CreateController(new FakeInquiryService(), new FakeCrmSyncService { RetryResult = null });

        var result = await controller.RetryCrmSync(999, CancellationToken.None);

        var problem = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, problem.StatusCode);
    }


}
