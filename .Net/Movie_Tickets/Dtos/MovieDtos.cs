// Dtos/Movie/MovieDtos.cs
using System.ComponentModel.DataAnnotations;

namespace Movie_Tickets.Dtos.MovieDtos;

public record MovieDto(int Id, string Title, string Description, int DurationMinutes, string? ImageUrl);

public class CreateMovieDto
{
    [Required, MinLength(1)] public string Title { get; set; } = "";
    [Required, MinLength(1)] public string Description { get; set; } = "";
    [Range(1, 1000)] public int DurationMinutes { get; set; }
    public string? ImageUrl { get; set; }
    public IFormFile? Poster { get; set; }
}

public class UpdateMovieDto : CreateMovieDto
{
    [Required] public int Id { get; set; }
}
