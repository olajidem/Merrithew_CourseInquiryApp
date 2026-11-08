namespace CourseInquiry.Api.Models;

using System.ComponentModel.DataAnnotations;

public class Inquiry
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(255)]
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

    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = InquiryStatus.New.ToString();

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedDate { get; set; }

    /// Soft delete: null = active, set = archived (hidden from all normal queries)
    public DateTime? ArchivedDate { get; set; }

}
