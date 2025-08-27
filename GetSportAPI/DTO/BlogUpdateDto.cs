using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GetSportAPI.DTO
{
    public class BlogUpdateDto
    {
        [StringLength(100, MinimumLength = 3, ErrorMessage = "Title must be between 3 and 100 characters.")]
        public string? Title { get; set; }

        [StringLength(255, MinimumLength = 10, ErrorMessage = "Short description must be between 10 and 255 characters.")]
        public string? Shortdesc { get; set; }

        [StringLength(5000, MinimumLength = 20, ErrorMessage = "Content must be between 20 and 5000 characters.")]
        public string? Content { get; set; }

        [RegularExpression("^(Draft|Published|Banned|Deleted)$", ErrorMessage = "Status must be 'Draft', 'Published','Banned' or 'Deleted'.")]
        public string? Status { get; set; }

        public IFormFile? Image { get; set; }
    }
}
