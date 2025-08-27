using System.ComponentModel.DataAnnotations;

namespace GetSportAPI.DTO
{
    public class BlogCreateDto
    {
        [Required(ErrorMessage = "Title is required.")]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "Title must be between 3 and 100 characters.")]
        public string Title { get; set; } = null!;

        [Required(ErrorMessage = "Short description is required.")]
        [StringLength(255, MinimumLength = 10, ErrorMessage = "Short description must be between 10 and 255 characters.")]
        public string Shortdesc { get; set; } = null!;

        [Required(ErrorMessage = "Content is required.")]
        [StringLength(5000, MinimumLength = 20, ErrorMessage = "Content must be between 20 and 5000 characters.")]
        public string Content { get; set; } = null!;

        public IFormFile? Image { get; set; }
    }
}
