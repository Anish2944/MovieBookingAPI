// Dtos/CreateTheaterDto.cs
using System.ComponentModel.DataAnnotations;

namespace Movie_Tickets.Dtos
{
    public class CreateTheaterDto
    {
        [Required, MinLength(1)]
        public string Name { get; set; } = default!;

        [Required, MinLength(1)]
        public string Location { get; set; } = default!;
    }
}
