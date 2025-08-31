// Dtos/GenerateSeatsDto.cs
namespace Movie_Tickets.Dtos
{
    public class GenerateSeatsDto
    {
        public int ScreenId { get; set; }
        public int Rows { get; set; }       // e.g. 10
        public int SeatsPerRow { get; set; } // e.g. 12
        public int? StartRowAscii { get; set; } = 65; // 'A' by default
    }
}
