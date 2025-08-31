using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Movie_Tickets.Data;

namespace MovieBooking.Api.Controllers;

[ApiController]
[Route("api/movies")]
public class MoviesController : ControllerBase
{
    private readonly AppDbContext _db;
    public MoviesController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll()
        => Ok(await _db.Movies.AsNoTracking().ToListAsync());

    [HttpPost]
    public async Task<IActionResult> Create(Movie movie)
    {
        _db.Movies.Add(movie);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = movie.Id }, movie);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var m = await _db.Movies.FindAsync(id);
        return m is null ? NotFound() : Ok(m);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, Movie updatedMovie)
    {
        if (id != updatedMovie.Id)
            return BadRequest("ID mismatch");
        var movie = await _db.Movies.FindAsync(id);
        if (movie is null)
            return NotFound();
        movie.Title = updatedMovie.Title;
        movie.Description = updatedMovie.Description;
        movie.DurationMinutes = updatedMovie.DurationMinutes;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var movie = await _db.Movies.FindAsync(id);
        if (movie is null)
            return NotFound();
        _db.Movies.Remove(movie);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
