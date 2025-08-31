// Dtos/CreateScreenDto.cs
namespace Movie_Tickets.Dtos
{
    public class CreateScreenDto
    {
        public int TheaterId { get; set; }
        public string Name { get; set; } = default!;
        public int TotalSeats { get; set; }  // overall count (optional if you auto-generate seats)
    }
}
