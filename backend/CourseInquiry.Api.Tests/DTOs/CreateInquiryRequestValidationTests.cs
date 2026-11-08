namespace CourseInquiry.Api.Tests.DTOs;

using System.ComponentModel.DataAnnotations;
using CourseInquiry.Api.DTOs;

// Runs the same DataAnnotations rules that [ApiController] applies before the controller is called
public class CreateInquiryRequestValidationTests
{
    private static CreateInquiryRequest ValidRequest() => new()
    {
        FirstName = "Jane",
        LastName = "Doe",
        Email = "jane@example.com",
        CourseName = "Intro to C#"
    };

    private static List<ValidationResult> Validate(CreateInquiryRequest request)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);
        return results;
    }

    private static bool HasErrorFor(List<ValidationResult> results, string member) =>
        results.Any(r => r.MemberNames.Contains(member));

    [Fact]
    public void ValidRequest_PassesValidation()
    {
        Assert.Empty(Validate(ValidRequest()));
    }

    [Theory]
    [InlineData(nameof(CreateInquiryRequest.FirstName))]
    [InlineData(nameof(CreateInquiryRequest.LastName))]
    [InlineData(nameof(CreateInquiryRequest.Email))]
    [InlineData(nameof(CreateInquiryRequest.CourseName))]
    public void RequiredField_WhitespaceOnly_FailsValidation(string field)
    {
        var request = ValidRequest();
        typeof(CreateInquiryRequest).GetProperty(field)!.SetValue(request, "   ");

        Assert.True(HasErrorFor(Validate(request), field));
    }

    [Theory]
    [InlineData("jane@localhost")]       // no dot in domain
    [InlineData("a@b")]                  // no TLD
    [InlineData("jane doe@example.com")] // space
    [InlineData("jane@example.c")]       // 1-char TLD
    [InlineData("jane.example.com")]     // no @
    public void Email_InvalidFormat_FailsValidation(string email)
    {
        var request = ValidRequest();
        request.Email = email;

        Assert.True(HasErrorFor(Validate(request), nameof(CreateInquiryRequest.Email)));
    }

    [Theory]
    [InlineData("jane@example.com")]
    [InlineData("jane.doe+courses@school.co.uk")]
    public void Email_ValidFormat_PassesValidation(string email)
    {
        var request = ValidRequest();
        request.Email = email;

        Assert.False(HasErrorFor(Validate(request), nameof(CreateInquiryRequest.Email)));
    }

    [Fact]
    public void Message_Over2000Characters_FailsValidation()
    {
        var request = ValidRequest();
        request.Message = new string('a', 2001);

        Assert.True(HasErrorFor(Validate(request), nameof(CreateInquiryRequest.Message)));
    }
}
