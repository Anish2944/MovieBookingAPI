// Dtos/CreateScreenDto.cs
using System.ComponentModel.DataAnnotations;

namespace Movie_Tickets.Dtos
{
    public class CreateScreenDto
    {
        [Range(1, int.MaxValue)]
        public int TheaterId { get; set; }

        [Required, MinLength(1)]
        public string Name { get; set; } = default!;

        [Range(0, int.MaxValue)]
        public int TotalSeats { get; set; }
    }
}
