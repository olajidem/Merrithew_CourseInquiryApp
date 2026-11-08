
using CourseInquiry.Api.Data;
using CourseInquiry.Api.Services;
using Microsoft.EntityFrameworkCore;
using CourseInquiry.Api.Integrations.Crm;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<AppDbContext>(options =>
options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddControllers();
builder.Services.AddScoped<IInquiryService, InquiryService>();

// CRM integration (simulated). Swap MockCrmClient for a real HttpClient-based client later.
builder.Services.Configure<CrmOptions>(builder.Configuration.GetSection(CrmOptions.SectionName));
builder.Services.AddSingleton<ICrmClient, MockCrmClient>();
builder.Services.AddScoped<ICrmSyncService, CrmSyncService>();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    policy.WithOrigins("http://localhost:5173")
    .AllowAnyHeader()
    .AllowAnyMethod());
});
builder.Services.AddProblemDetails();

var app = builder.Build();
app.UseExceptionHandler();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors("Frontend");

app.UseAuthorization();

app.MapControllers();

app.Run();
