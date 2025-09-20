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
    private const string ConfirmedStatus = "Confirmed";
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
        if (showId <= 0)
        {
            return BadRequest(new ApiResponse<object>(false, null, "Invalid show ID"));
        }

        if (dto.Price < 0)
        {
            return BadRequest(new ApiResponse<object>(false, null, "Price cannot be negative"));
        }

        var show = await _db.Shows.FindAsync(showId);
        if (show is null)
        {
            return NotFound(new ApiResponse<object>(false, null, $"Show {showId} not found"));
        }

        var movie = await _db.Movies.AsNoTracking().FirstOrDefaultAsync(m => m.Id == dto.MovieId);
        if (movie is null)
        {
            return NotFound(new ApiResponse<object>(false, null, $"Movie {dto.MovieId} not found"));
        }

        var screenExists = await _db.Screens.AnyAsync(s => s.Id == dto.ScreenId);
        if (!screenExists)
        {
            return NotFound(new ApiResponse<object>(false, null, $"Screen {dto.ScreenId} not found"));
        }

        if (dto.StartsAtUtc <= DateTime.UtcNow)
        {
            return BadRequest(new ApiResponse<object>(false, null, "Start time must be in the future"));
        }

        var showStart = DateTime.SpecifyKind(dto.StartsAtUtc, DateTimeKind.Utc);
        var showEnd = showStart.AddMinutes(movie.DurationMinutes);

        var overlap = await _db.Shows
            .Where(s => s.ScreenId == dto.ScreenId && s.Id != showId)
            .Join(_db.Movies, s => s.MovieId, m => m.Id, (s, m) => new { s.StartsAtUtc, Duration = m.DurationMinutes })
            .AnyAsync(x => ShowTimeHelper.Overlaps(showStart, showEnd, x.StartsAtUtc, x.StartsAtUtc.AddMinutes(x.Duration)));

        if (overlap)
        {
            return Conflict(new ApiResponse<object>(false, null, "Overlapping show on this screen"));
        }

        show.MovieId = dto.MovieId;
        show.ScreenId = dto.ScreenId;
        show.StartsAtUtc = showStart;
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
        var show = await _db.Shows
            .AsNoTracking()
            .Where(s => s.Id == showId)
            .Select(s => new { s.Id, s.ScreenId })
            .FirstOrDefaultAsync();

        if (show is null)
        {
            return NotFound(new ApiResponse<object>(false, null, $"Show {showId} not found"));
        }

        var seats = await _db.Seats
            .Where(seat => seat.ScreenId == show.ScreenId)
            .OrderBy(seat => seat.Row)
            .ThenBy(seat => seat.Number)
            .Select(seat => new { seat.Id, seat.Row, seat.Number })
            .ToListAsync();

        var confirmedSeatIds = await _db.BookingSeats
            .Where(bs => bs.Booking.ShowId == showId && bs.Booking.Status == ConfirmedStatus)
            .Select(bs => bs.SeatId)
            .ToListAsync();

        var lockedSeatIds = await _db.SeatLocks
            .Where(l => l.ShowId == showId && l.ExpiresAtUtc > DateTime.UtcNow)
            .Select(l => l.SeatId)
            .ToListAsync();

        var confirmed = new HashSet<int>(confirmedSeatIds);
        var locked = new HashSet<int>(lockedSeatIds);

        var response = seats.Select(seat => new
        {
            seat.Id,
            seat.Row,
            seat.Number,
            IsBooked = confirmed.Contains(seat.Id),
            IsLocked = locked.Contains(seat.Id)
        });

        return Ok(response);
    }
}
