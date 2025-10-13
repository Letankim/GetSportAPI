using System.ComponentModel.DataAnnotations;

namespace GetSportAPI.DTO
{
    public class PlaymatePostUpdateDto
    {
        [StringLength(100, ErrorMessage = "Title cannot exceed 100 characters.")]
        public string? Title { get; set; }

        [StringLength(500, ErrorMessage = "Content cannot exceed 500 characters.")]
        public string? Content { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Needed players must be greater than 0.")]
        public int? Neededplayers { get; set; }

        [StringLength(50, ErrorMessage = "Skill level cannot exceed 50 characters.")]
        public string? Skilllevel { get; set; }

        [StringLength(50, ErrorMessage = "Status cannot exceed 50 characters.")]
        public string? Status { get; set; }
    }
}
