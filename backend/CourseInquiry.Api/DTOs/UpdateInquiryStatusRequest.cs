namespace CourseInquiry.Api.DTOs;

using System.ComponentModel.DataAnnotations;

public class UpdateInquiryStatusRequest
{
    [Required(ErrorMessage = "Status is required.")]
    public string Status { get; set; } = string.Empty;
}
