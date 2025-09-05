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
    public async Task<IActionResult> Create(CreateMovieDto dto)
    {
        var movie = new Movie
        {
            Title = dto.Title,
            Description = dto.Description,
            DurationMinutes = dto.DurationMinutes,
            ImageUrl = dto.ImageUrl // ✅ can be provided directly, or later updated via upload API
        };

        _db.Movies.Add(movie);
        await _db.SaveChangesAsync();

        var result = new MovieDto(movie.Id, movie.Title, movie.Description ?? "", movie.DurationMinutes, movie.ImageUrl);
        return CreatedAtAction(nameof(GetById), new { id = movie.Id }, result);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateMovieDto dto)
    {
        if (id != dto.Id)
            return BadRequest(new { message = "ID mismatch" });

        var movie = await _db.Movies.FindAsync(id);
        if (movie is null)
            return NotFound(new { message = "Movie not found" });

        movie.Title = dto.Title;
        movie.Description = dto.Description;
        movie.DurationMinutes = dto.DurationMinutes;
        movie.ImageUrl = dto.ImageUrl; // ✅ update image URL

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

    public class MovieUploadDto
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int DurationMinutes { get; set; }
        public IFormFile Poster { get; set; } = null!;
    }

    [HttpPost("upload")]
    [Consumes("multipart/form-data")] // important for Swagger
    public async Task<IActionResult> Upload([FromForm] MovieUploadDto dto)
    {
        if (dto.Poster == null || dto.Poster.Length == 0)
            return BadRequest("No file uploaded");

        var filePath = Path.Combine("uploads", dto.Poster.FileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await dto.Poster.CopyToAsync(stream);
        }

        var movie = new Movie
        {
            Title = dto.Title,
            Description = dto.Description,
            DurationMinutes = dto.DurationMinutes,
            ImageUrl = filePath
        };

        _db.Movies.Add(movie);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = movie.Id }, movie);
    }


}
