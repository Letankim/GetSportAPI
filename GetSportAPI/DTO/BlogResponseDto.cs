using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GetSportAPI.DTO
{
    public class BlogResponseDto
    {
        public int BlogId { get; set; }
        public string Title { get; set; } = null!;
        public string Shortdesc { get; set; } = null!;
        public string Content { get; set; } = null!;
        public string? Imageurl { get; set; }
        public string Status { get; set; } = null!;
        public DateTime Createdat { get; set; }
        public DateTime? Updatedat { get; set; }
        public int AccountId { get; set; }
        public string AuthorName { get; set; } = null!;
    }
}
