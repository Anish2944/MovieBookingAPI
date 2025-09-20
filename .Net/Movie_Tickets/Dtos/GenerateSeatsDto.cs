// Dtos/GenerateSeatsDto.cs
using System.ComponentModel.DataAnnotations;

namespace Movie_Tickets.Dtos
{
    public class GenerateSeatsDto
    {
        [Range(1, int.MaxValue)]
        public int ScreenId { get; set; }

        [Range(1, 26)]
        public int Rows { get; set; }

        [Range(1, 100)]
        public int SeatsPerRow { get; set; }

        [Range(65, 90)]
        public int? StartRowAscii { get; set; } = 65; // 'A'
    }
}
