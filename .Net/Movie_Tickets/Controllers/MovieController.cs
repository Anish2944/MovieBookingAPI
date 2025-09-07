// Controllers/MoviesController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Movie_Tickets.Data;
using Movie_Tickets.Dtos.MovieDtos;

[ApiController]
[Route("api/movies")]
public class MoviesController : ControllerBase
{
    private readonly AppDbContext _db;
    public MoviesController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var data = await _db.Movies.AsNoTracking()
            .Select(m => new MovieDto(m.Id, m.Title, m.Description ?? "", m.DurationMinutes, m.ImageUrl))
            .ToListAsync();
        return Ok(data);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var m = await _db.Movies.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(m => new MovieDto(m.Id, m.Title, m.Description ?? "", m.DurationMinutes, m.ImageUrl))
            .FirstOrDefaultAsync();

        return m is null ? NotFound(new { message = "Movie not found" }) : Ok(m);
    }

    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Create([FromForm] CreateMovieDto dto)
    {
        string? imageUrl = dto.ImageUrl;

        if (dto.Poster != null && dto.Poster.Length > 0)
        {
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var fileName = Guid.NewGuid() + Path.GetExtension(dto.Poster.FileName);
            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
                await dto.Poster.CopyToAsync(stream);

            imageUrl = $"/uploads/{fileName}";
        }

        var movie = new Movie
        {
            Title = dto.Title,
            Description = dto.Description,
            DurationMinutes = dto.DurationMinutes,
            ImageUrl = imageUrl
        };

        _db.Movies.Add(movie);
        await _db.SaveChangesAsync();

        var result = new MovieDto(movie.Id, movie.Title, movie.Description ?? "", movie.DurationMinutes, movie.ImageUrl);
        return CreatedAtAction(nameof(GetById), new { id = movie.Id }, result);
    }

    [HttpPut("{id:int}")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Update(int id, [FromForm] UpdateMovieDto dto)
    {
        var movie = await _db.Movies.FindAsync(id);
        if (movie is null) return NotFound(new { message = "Movie not found" });

        movie.Title = dto.Title;
        movie.Description = dto.Description;
        movie.DurationMinutes = dto.DurationMinutes;

        if (dto.Poster != null && dto.Poster.Length > 0)
        {
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var fileName = Guid.NewGuid() + Path.GetExtension(dto.Poster.FileName);
            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
                await dto.Poster.CopyToAsync(stream);

            movie.ImageUrl = $"/uploads/{fileName}";
        }
        else if (!string.IsNullOrEmpty(dto.ImageUrl))
        {
            movie.ImageUrl = dto.ImageUrl;
        }

        await _db.SaveChangesAsync();

        var result = new MovieDto(movie.Id, movie.Title, movie.Description ?? "", movie.DurationMinutes, movie.ImageUrl);
        return Ok(result);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var movie = await _db.Movies.FindAsync(id);
        if (movie is null)
            return NotFound(new { message = "Movie not found" });

        _db.Movies.Remove(movie);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
