// Controllers/MoviesController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Movie_Tickets.Data;
using Movie_Tickets.Common;
using Movie_Tickets.Dtos.MovieDtos;

[ApiController]
[Route("api/movies")]
public class MoviesController : ControllerBase
{
    private readonly AppDbContext _db;
    public MoviesController(AppDbContext db) => _db = db;

    // Fix CS8604 by ensuring Description is never null when constructing MovieDto
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var data = await _db.Movies.AsNoTracking()
            .Select(m => new MovieDto(m.Id, m.Title, m.Description ?? string.Empty, m.DurationMinutes))
            .ToListAsync();
        return Ok(new ApiResponse<object>(true, data));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var m = await _db.Movies.AsNoTracking()
            .Where(x => x.Id == id)
            .Select(m => new MovieDto(m.Id, m.Title, m.Description ?? string.Empty, m.DurationMinutes))
            .FirstOrDefaultAsync();
        return m is null
            ? NotFound(new ApiResponse<object>(false, null, "Movie not found"))
            : Ok(new ApiResponse<MovieDto>(true, m));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateMovieDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(new ApiResponse<object>(false, null, "Invalid input"));

        var movie = new Movie { Title = dto.Title, Description = dto.Description, DurationMinutes = dto.DurationMinutes };
        _db.Movies.Add(movie);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = movie.Id },
            new ApiResponse<MovieDto>(true, new(movie.Id, movie.Title, movie.Description, movie.DurationMinutes)));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateMovieDto dto)
    {
        if (id != dto.Id) return BadRequest(new ApiResponse<object>(false, null, "ID mismatch"));
        if (!ModelState.IsValid) return BadRequest(new ApiResponse<object>(false, null, "Invalid input"));

        var movie = await _db.Movies.FindAsync(id);
        if (movie is null) return NotFound(new ApiResponse<object>(false, null, "Movie not found"));

        movie.Title = dto.Title; movie.Description = dto.Description; movie.DurationMinutes = dto.DurationMinutes;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var movie = await _db.Movies.FindAsync(id);
        if (movie is null) return NotFound(new ApiResponse<object>(false, null, "Movie not found"));
        _db.Movies.Remove(movie);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
