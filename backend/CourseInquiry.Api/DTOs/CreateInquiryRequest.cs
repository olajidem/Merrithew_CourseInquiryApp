namespace CourseInquiry.Api.DTOs;

using System.ComponentModel.DataAnnotations;

public class CreateInquiryRequest
{

    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(255)]
    [RegularExpression(@"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$",
        ErrorMessage = "Please enter a valid email address (e.g. name@example.com).")]
    public string Email { get; set; } = string.Empty;

    [Phone]
    [MaxLength(20)]
    public string? Phone { get; set; }

    [Required]
    [MaxLength(150)]
    public string CourseName { get; set; } = string.Empty;

    [MaxLength(150)]
    public string? PreferredLocation { get; set; }

    [MaxLength(2000)]
    public string? Message { get; set; }

}
