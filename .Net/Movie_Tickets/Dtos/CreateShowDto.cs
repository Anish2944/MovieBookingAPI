// Dtos/CreateShowDto.cs
namespace Movie_Tickets.Dtos
{
    public class CreateShowDto
    {
        public int MovieId { get; set; }
        public int ScreenId { get; set; }
        public DateTime StartsAtUtc { get; set; }
        public decimal Price { get; set; }
    }
}
