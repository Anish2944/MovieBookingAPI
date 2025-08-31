using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Movie_Tickets.Data;
using Movie_Tickets.Dtos;

namespace Movie_Tickets.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TheatersController : ControllerBase
{
    private readonly AppDbContext _db;
    public TheatersController(AppDbContext db) => _db = db;

    // POST /api/theaters
    [HttpPost]
    public async Task<ActionResult<Theater>> CreateTheater([FromBody] CreateTheaterDto dto)
    {
        var theater = new Theater { Name = dto.Name, Location = dto.Location };
        _db.Theaters.Add(theater);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetTheater), new { id = theater.Id }, theater);
    }

    // GET /api/theaters/{id}
    [HttpGet("{id:int}")]
    public async Task<ActionResult<Theater>> GetTheater(int id)
    {
        var t = await _db.Theaters
            .Include(x => x.Screens)
            .ThenInclude(s => s.Seats)
            .FirstOrDefaultAsync(x => x.Id == id);
        return t is null ? NotFound() : Ok(t);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult> UpdateTheater(int id, [FromBody] CreateTheaterDto dto)
    {
        var theater = await _db.Theaters.FindAsync(id);
        if (theater is null) return NotFound();
        theater.Name = dto.Name;
        theater.Location = dto.Location;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult> DeleteTheater(int id)
    {
        var theater = await _db.Theaters.FindAsync(id);
        if (theater is null) return NotFound();
        _db.Theaters.Remove(theater);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // GET /api/theaters
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Theater>>> List()
        => Ok(await _db.Theaters.AsNoTracking().ToListAsync());

    // POST /api/theaters/screen
    [HttpPost("screen")]
    public async Task<ActionResult<Screen>> CreateScreen([FromBody] CreateScreenDto dto)
    {
        var theaterExists = await _db.Theaters.AnyAsync(t => t.Id == dto.TheaterId);
        if (!theaterExists) return NotFound($"Theater {dto.TheaterId} not found");

        var screen = new Screen
        {
            TheaterId = dto.TheaterId,
            Name = dto.Name,
            TotalSeats = dto.TotalSeats
        };
        _db.Screens.Add(screen);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetScreen), new { id = screen.Id }, screen);
    }

    // GET /api/theaters/screen/{id}
    [HttpGet("screen/{id:int}")]
    public async Task<ActionResult<Screen>> GetScreen(int id)
    {
        var s = await _db.Screens
            .Include(x => x.Seats)
            .Include(x => x.Theater)
            .FirstOrDefaultAsync(x => x.Id == id);
        return s is null ? NotFound() : Ok(s);
    }

    // POST /api/theaters/generate-seats
    // Generates a tidy grid: A1..A{n}, B1..B{n}, etc.
    [HttpPost("generate-seats")]
    public async Task<ActionResult> GenerateSeats([FromBody] GenerateSeatsDto dto)
    {
        var screen = await _db.Screens.FirstOrDefaultAsync(s => s.Id == dto.ScreenId);
        if (screen is null) return NotFound($"Screen {dto.ScreenId} not found");

        var start = dto.StartRowAscii ?? 65; // 'A'
        var seats = new List<Seat>();
        for (int r = 0; r < dto.Rows; r++)
        {
            var rowLabel = ((char)(start + r)).ToString();
            for (int c = 1; c <= dto.SeatsPerRow; c++)
            {
                seats.Add(new Seat
                {
                    ScreenId = screen.Id,
                    Row = rowLabel,
                    Number = c,
                    IsDisabled = false
                });
            }
        }

        _db.Seats.AddRange(seats);
        screen.TotalSeats = await _db.Seats.CountAsync(s => s.ScreenId == screen.Id) + seats.Count;
        await _db.SaveChangesAsync();
        return Ok(new { created = seats.Count });
    }
}
