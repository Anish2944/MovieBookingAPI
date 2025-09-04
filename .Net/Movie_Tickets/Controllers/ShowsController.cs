using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Movie_Tickets.Data;
using Movie_Tickets.Dtos;
using Movie_Tickets.Common;

namespace Movie_Tickets.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ShowsController : ControllerBase
{
    private readonly AppDbContext _db;
    public ShowsController(AppDbContext db) => _db = db;

    // POST /api/shows
    [HttpPost]
    public async Task<ActionResult<Show>> Create([FromBody] CreateShowDto dto)
    {
        if (dto.Price < 0) return BadRequest(new ApiResponse<object>(false, null, "Price cannot be negative"));

        var movie = await _db.Movies.AsNoTracking().FirstOrDefaultAsync(m => m.Id == dto.MovieId);
        if (movie is null) return NotFound(new ApiResponse<object>(false, null, $"Movie {dto.MovieId} not found"));

        var screenExists = await _db.Screens.AnyAsync(s => s.Id == dto.ScreenId);
        if (!screenExists) return NotFound($"Screen {dto.ScreenId} not found");

        if (dto.StartsAtUtc <= DateTime.UtcNow)
            return BadRequest(new ApiResponse<object>(false, null, "Start time must be in the future"));

        var showStart = DateTime.SpecifyKind(dto.StartsAtUtc, DateTimeKind.Utc);
        var showEnd = showStart.AddMinutes(movie.DurationMinutes);

        // prevent overlaps on the same screen
        var overlap = await _db.Shows
            .Where(s => s.ScreenId == dto.ScreenId)
            .Join(_db.Movies, s => s.MovieId, m => m.Id, (s, m) => new { s.StartsAtUtc, Duration = m.DurationMinutes })
            .AnyAsync(x => ShowTimeHelper.Overlaps(showStart, showEnd, x.StartsAtUtc, x.StartsAtUtc.AddMinutes(x.Duration)));

        if (overlap)
            return Conflict(new ApiResponse<object>(false, null, "Overlapping show on this screen"));

        var show = new Show
        {
            MovieId = dto.MovieId,
            ScreenId = dto.ScreenId,
            StartsAtUtc = DateTime.SpecifyKind(dto.StartsAtUtc, DateTimeKind.Utc),
            Price = dto.Price
        };

        _db.Shows.Add(show);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { showId = show.Id }, show);
    }

    [HttpGet]
    public async Task<ActionResult<List<Show>>> GetAll()
    {
        var shows = await _db.Shows
            .Include(s => s.Movie)
            .Include(s => s.Screen).ThenInclude(sc => sc.Theater)
            .ToListAsync();
        return Ok(shows);
    }

    // GET /api/shows/{showId}
    [HttpGet("{showId:int}")]
    public async Task<ActionResult<Show>> GetById(int showId)
    {
        var show = await _db.Shows
            .Include(s => s.Movie)
            .Include(s => s.Screen).ThenInclude(sc => sc.Theater)
            .FirstOrDefaultAsync(s => s.Id == showId);

        return show is null ? NotFound() : Ok(show);
    }

    [HttpPut("{showId:int}")]
    public async Task<IActionResult> Update(int showId, [FromBody] CreateShowDto dto)
    {
        if (showId <= 0) return BadRequest("Invalid show ID");
        var show = await _db.Shows.FindAsync(showId);
        if (show is null) return NotFound($"Show {showId} not found");
        var movieExists = await _db.Movies.AnyAsync(m => m.Id == dto.MovieId);
        if (!movieExists) return NotFound($"Movie {dto.MovieId} not found");
        var screenExists = await _db.Screens.AnyAsync(s => s.Id == dto.ScreenId);
        if (!screenExists) return NotFound($"Screen {dto.ScreenId} not found");
        show.MovieId = dto.MovieId;
        show.ScreenId = dto.ScreenId;
        show.StartsAtUtc = DateTime.SpecifyKind(dto.StartsAtUtc, DateTimeKind.Utc);
        show.Price = dto.Price;
        await _db.SaveChangesAsync();
        return NoContent();
    }
    [HttpDelete("{showId:int}")]
    public async Task<IActionResult> Delete(int showId)
    {
        var show = await _db.Shows.FindAsync(showId);
        if (show is null) return NotFound($"Show {showId} not found");
        _db.Shows.Remove(show);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // GET /api/shows/by-movie/{movieId}
    [HttpGet("by-movie/{movieId:int}")]
    public async Task<IActionResult> ByMovie(int movieId)
    {
        var shows = await _db.Shows
            .Where(s => s.MovieId == movieId && s.StartsAtUtc > DateTime.UtcNow.AddHours(-1))
            .Select(s => new {
                s.Id,
                s.StartsAtUtc,
                s.Price,
                Screen = s.Screen.Name,
                Theater = s.Screen.Theater.Name
            })
            .ToListAsync();

        return Ok(shows);
    }

    // GET /api/shows/{showId}/seats
    [HttpGet("{showId:int}/seats")]
    public async Task<IActionResult> Seats(int showId)
    {
        var screenId = await _db.Shows
            .Where(s => s.Id == showId)
            .Select(s => s.ScreenId)
            .FirstOrDefaultAsync();

        if (screenId == default)
            return NotFound($"Show {showId} not found");

        var seats = await _db.Seats
            .Where(seat => seat.ScreenId == screenId)
            .Select(seat => new {
                seat.Id,
                seat.Row,
                seat.Number,
                IsBooked = _db.BookingSeats.Any(bs => bs.SeatId == seat.Id &&
                    _db.Bookings.Any(b => b.Id == bs.BookingId && b.ShowId == showId && b.Status == "Confirmed")),
                IsLocked = _db.SeatLocks.Any(l => l.SeatId == seat.Id && l.ShowId == showId && l.ExpiresAtUtc > DateTime.UtcNow)
            })
            .ToListAsync();

        return Ok(seats);
    }
}
