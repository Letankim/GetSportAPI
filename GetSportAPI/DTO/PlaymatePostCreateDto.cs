using System.ComponentModel.DataAnnotations;

namespace GetSportAPI.DTO
{
    public class PlaymatePostCreateDto
    {
        [Required(ErrorMessage = "Title is required.")]
        [StringLength(100, ErrorMessage = "Title cannot exceed 100 characters.")]
        public string Title { get; set; } = null!;

        [StringLength(500, ErrorMessage = "Content cannot exceed 500 characters.")]
        public string? Content { get; set; }

        [Required(ErrorMessage = "Needed players is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Needed players must be greater than 0.")]
        public int Neededplayers { get; set; }

        [StringLength(50, ErrorMessage = "Skill level cannot exceed 50 characters.")]
        public string? Skilllevel { get; set; }

        [Required(ErrorMessage = "Status is required.")]
        [StringLength(50, ErrorMessage = "Status cannot exceed 50 characters.")]
        public string Status { get; set; } = null!;

        [Required(ErrorMessage = "Court booking ID is required.")]
        public int CourtbookingId { get; set; }
    }
}
