namespace GetSportAPI.DTO
{
    public class AuthResponseDto
    {
        public string Token { get; set; } = null!;
        public string Fullname { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string Role { get; set; } = null!;
    }
}
