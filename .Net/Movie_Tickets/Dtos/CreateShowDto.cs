// Dtos/CreateShowDto.cs
using System.ComponentModel.DataAnnotations;

namespace Movie_Tickets.Dtos
{
    public class CreateShowDto
    {
        [Range(1, int.MaxValue)]
        public int MovieId { get; set; }

        [Range(1, int.MaxValue)]
        public int ScreenId { get; set; }

        [DataType(DataType.DateTime)]
        public DateTime StartsAtUtc { get; set; }

        [Range(0, double.MaxValue)]
        public decimal Price { get; set; }
    }
}
