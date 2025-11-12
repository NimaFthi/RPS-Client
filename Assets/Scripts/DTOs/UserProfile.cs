namespace DTOs
{
    public class UserProfile : BaseDto
    {
        public string UserId { get; set; }
        public string Username { get; set; }
        public int CoinsCount { get; set; }
        public int WinsCount { get; set; }
        public int LossesCount { get; set; }
    }
}