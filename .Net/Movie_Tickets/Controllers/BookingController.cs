using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Movie_Tickets.Common;
using Movie_Tickets.Data;

namespace Movie_Tickets.Controllers;

[ApiController]
[Route("api/bookings")]
[Authorize]
public class BookingsController : ControllerBase
{
    private const string ConfirmedStatus = "Confirmed";
    private readonly AppDbContext _db;

    public BookingsController(AppDbContext db) => _db = db;

    public record SeatSelectionRequest(int ShowId, int[] SeatIds);

    [HttpPost("lock")] 
    public async Task<IActionResult> LockSeats([FromBody] SeatSelectionRequest req)
    {
  
        var email = User.GetEmail();
        if (string.IsNullOrWhiteSpace(email))
        {
            return Unauthorized(new ApiResponse<object>(false, null, "Not logged in"));
        }

        if (req is null || req.SeatIds is null || req.SeatIds.Length == 0)
        {
            return BadRequest(new ApiResponse<object>(false, null, "Seat selection is required"));
        }

        var seatIds = req.SeatIds.Where(id => id > 0).Distinct().ToArray();
        if (seatIds.Length == 0)
        {
            return BadRequest(new ApiResponse<object>(false, null, "Seat selection is required"));
        }

        var show = await _db.Shows.AsNoTracking().FirstOrDefaultAsync(s => s.Id == req.ShowId);
        if (show is null)
        {
            return NotFound(new ApiResponse<object>(false, null, "Show not found"));
        }

        var validSeatIds = await _db.Seats
            .Where(se => se.ScreenId == show.ScreenId && seatIds.Contains(se.Id))
            .Select(se => se.Id)
            .ToListAsync();

        if (validSeatIds.Count != seatIds.Length)
        {
            return BadRequest(new ApiResponse<object>(false, null, "Some seats are invalid for this show"));
        }

        var now = DateTime.UtcNow;
        var normalizedEmail = email.Trim().ToLowerInvariant();

        var bookedSeatIds = await _db.BookingSeats
            .Where(bs => seatIds.Contains(bs.SeatId) && bs.Booking.ShowId == req.ShowId && bs.Booking.Status == ConfirmedStatus)
            .Select(bs => bs.SeatId)
            .ToListAsync();

        if (bookedSeatIds.Count > 0)
        {
            return Conflict(new ApiResponse<object>(false, null, $"Seats already booked: {string.Join(", ", bookedSeatIds)}"));
        }

        var existingLocks = await _db.SeatLocks
            .Where(l => l.ShowId == req.ShowId && seatIds.Contains(l.SeatId))
            .ToListAsync();

        var expiredLocks = existingLocks.Where(l => l.ExpiresAtUtc <= now).ToList();
        if (expiredLocks.Count > 0)
        {
            _db.SeatLocks.RemoveRange(expiredLocks);
        }

        var activeLocks = existingLocks.Where(l => l.ExpiresAtUtc > now).ToList();
        var conflictingLocks = activeLocks
            .Where(l => !string.Equals(l.LockedBy, normalizedEmail, StringComparison.OrdinalIgnoreCase))
            .Select(l => l.SeatId)
            .Distinct()
            .ToList();

        if (conflictingLocks.Count > 0)
        {
            return Conflict(new ApiResponse<object>(false, null, $"Seats already locked: {string.Join(", ", conflictingLocks)}"));
        }

        var expires = now.AddMinutes(5);

        foreach (var lockEntry in activeLocks.Where(l => string.Equals(l.LockedBy, normalizedEmail, StringComparison.OrdinalIgnoreCase)))
        {
            lockEntry.ExpiresAtUtc = expires;
        }

        var seatsToLock = seatIds.Except(activeLocks.Select(l => l.SeatId)).ToList();
        if (seatsToLock.Count > 0)
        {
            var newLocks = seatsToLock.Select(seatId => new SeatLock
            {
                ShowId = req.ShowId,
                SeatId = seatId,
                LockedBy = normalizedEmail,
                ExpiresAtUtc = expires
            });

            await _db.SeatLocks.AddRangeAsync(newLocks);
        }

        try
        {
            await _db.SaveChangesAsync();
            return Ok(new ApiResponse<object>(true, new { message = "locked", expiresAtUtc = expires }));
        }
        catch (DbUpdateException)
        {
            return Conflict(new ApiResponse<object>(false, null, "Some seats are already locked"));
        }
    }

    [HttpPost("confirm")]
    public async Task<IActionResult> Confirm([FromBody] SeatSelectionRequest req)
    {
        var email = User.GetEmail();
        var userId = User.GetUserId();

        if (string.IsNullOrWhiteSpace(email) || userId is null)
        {
            return Unauthorized(new ApiResponse<object>(false, null, "Not logged in"));
        }

        if (req is null || req.SeatIds is null || req.SeatIds.Length == 0)
        {
            return BadRequest(new ApiResponse<object>(false, null, "Seat selection is required"));
        }

        var seatIds = req.SeatIds.Where(id => id > 0).Distinct().ToArray();
        if (seatIds.Length == 0)
        {
            return BadRequest(new ApiResponse<object>(false, null, "Seat selection is required"));
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var now = DateTime.UtcNow;

        await using var tx = await _db.Database.BeginTransactionAsync();

        try
        {
            var locks = await _db.SeatLocks
                .Where(l => l.ShowId == req.ShowId && seatIds.Contains(l.SeatId))
                .ToListAsync();

            var validLocks = locks
                .Where(l => l.ExpiresAtUtc > now && string.Equals(l.LockedBy, normalizedEmail, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (validLocks.Count != seatIds.Length)
            {
                return Conflict(new ApiResponse<object>(false, null, "Missing or expired locks"));
            }

            var bookedSeatIds = await _db.BookingSeats
                .Where(bs => seatIds.Contains(bs.SeatId) && bs.Booking.ShowId == req.ShowId && bs.Booking.Status == ConfirmedStatus)
                .Select(bs => bs.SeatId)
                .ToListAsync();

            if (bookedSeatIds.Count > 0)
            {
                return Conflict(new ApiResponse<object>(false, null, $"Seats already booked: {string.Join(", ", bookedSeatIds)}"));
            }

            var show = await _db.Shows.AsNoTracking().FirstOrDefaultAsync(s => s.Id == req.ShowId);
            if (show is null)
            {
                return NotFound(new ApiResponse<object>(false, null, "Show not found"));
            }

            var booking = new Booking
            {
                ShowId = req.ShowId,
                UserId = userId.Value,
                Status = ConfirmedStatus,
                TotalAmount = show.Price * seatIds.Length,
                CreatedAtUtc = DateTime.UtcNow
            };

            _db.Bookings.Add(booking);

            foreach (var seatId in seatIds)
            {
                _db.BookingSeats.Add(new BookingSeat { Booking = booking, SeatId = seatId });
            }

            _db.SeatLocks.RemoveRange(validLocks);

            await _db.SaveChangesAsync();
            await tx.CommitAsync();

            return Ok(new ApiResponse<object>(true, new { bookingId = booking.Id, total = booking.TotalAmount }));
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    [HttpPost("release-expired-locks")]
    [AllowAnonymous]
    public async Task<IActionResult> Cleanup()
    {
        var now = DateTime.UtcNow;
        var expired = await _db.SeatLocks.Where(l => l.ExpiresAtUtc <= now).ToListAsync();
        if (expired.Count > 0)
        {
            _db.SeatLocks.RemoveRange(expired);
            await _db.SaveChangesAsync();
        }

        return Ok(new ApiResponse<object>(true, new { removed = expired.Count }));
    }

    [HttpGet("my")]
    public async Task<IActionResult> MyBookings()
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized(new ApiResponse<object>(false, null, "Not logged in"));
        }

        var bookings = await _db.Bookings
            .Where(b => b.UserId == userId.Value)
            .Include(b => b.Show).ThenInclude(s => s.Movie)
            .Include(b => b.Show).ThenInclude(s => s.Screen)
            .Include(b => b.BookingSeats).ThenInclude(bs => bs.Seat)
            .OrderByDescending(b => b.CreatedAtUtc)
            .AsNoTracking()
            .ToListAsync();

        var result = bookings.Select(b => new
        {
            b.Id,
            MovieTitle = b.Show.Movie.Title,
            ScreenName = b.Show.Screen.Name,
            b.Show.StartsAtUtc,
            b.Status,
            b.TotalAmount,
            Seats = b.BookingSeats.Select(bs => new { bs.Seat.Row, bs.Seat.Number })
        });

        return Ok(new ApiResponse<object>(true, result));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetBooking(int id)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized(new ApiResponse<object>(false, null, "Not logged in"));
        }

        var booking = await _db.Bookings
            .Where(b => b.Id == id && b.UserId == userId.Value)
            .Include(b => b.Show).ThenInclude(s => s.Movie)
            .Include(b => b.Show).ThenInclude(s => s.Screen)
            .Include(b => b.BookingSeats).ThenInclude(bs => bs.Seat)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (booking is null)
        {
            return NotFound(new ApiResponse<object>(false, null, "Booking not found"));
        }

        var result = new
        {
            booking.Id,
            MovieTitle = booking.Show.Movie.Title,
            ScreenName = booking.Show.Screen.Name,
            booking.Show.StartsAtUtc,
            booking.Status,
            booking.TotalAmount,
            Seats = booking.BookingSeats.Select(bs => new { bs.Seat.Row, bs.Seat.Number })
        };

        return Ok(new ApiResponse<object>(true, result));
    }
}
