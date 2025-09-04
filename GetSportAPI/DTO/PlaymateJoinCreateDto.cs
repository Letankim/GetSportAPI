using System.ComponentModel.DataAnnotations;

namespace GetSportAPI.DTO
{
    public class PlaymateJoinCreateDto
    {
        [Required(ErrorMessage = "Post ID is required.")]
        public int PostId { get; set; }
    }
}
