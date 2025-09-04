using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Movie_Tickets.Data;
using Movie_Tickets.Common;

namespace MovieBooking.Api.Controllers;

[ApiController]
[Route("api/bookings")]
public class BookingsController : ControllerBase
{
    private readonly AppDbContext _db;
    public BookingsController(AppDbContext db) => _db = db;

    public record LockSeatsRequest(int ShowId, int[] SeatIds, string UserEmail);
    public record ConfirmRequest(int ShowId, int[] SeatIds, string UserEmail);

    [HttpPost("lock")]
    public async Task<IActionResult> LockSeats(LockSeatsRequest req)
    {
        // Validate show & screen
        var show = await _db.Shows.AsNoTracking().FirstOrDefaultAsync(s => s.Id == req.ShowId);
        if (show is null) return NotFound(new ApiResponse<object>(false, null, "Show not found"));

        // Ensure all seats belong to the show's screen
        var validSeatIds = await _db.Seats.Where(se => se.ScreenId == show.ScreenId && req.SeatIds.Contains(se.Id))
                                          .Select(se => se.Id).ToListAsync();
        if (validSeatIds.Count != req.SeatIds.Distinct().Count())
            return BadRequest(new ApiResponse<object>(false, null, "Some seats are invalid for this show"));

        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(5);

        foreach (var seatId in req.SeatIds.Distinct())
        {
            _db.SeatLocks.Add(new SeatLock
            {
                ShowId = req.ShowId,
                SeatId = seatId,
                LockedBy = req.UserEmail, // or use User.GetEmail() if you enforce auth here
                ExpiresAtUtc = expires
            });
        }

        try
        {
            await _db.SaveChangesAsync();
            return Ok(new ApiResponse<object>(true, new { message = "locked", expiresAtUtc = expires }));
        }
        catch (DbUpdateException)
        {
            return Conflict(new ApiResponse<object>(false, null, "Some seats already locked"));
        }
    }

    [HttpPost("confirm")]
    public async Task<IActionResult> Confirm(ConfirmRequest req)
    {
        // (Optionally) pull user email from JWT, not body
        var jwtEmail = User.GetEmail();
        if (string.IsNullOrEmpty(jwtEmail) || !string.Equals(jwtEmail, req.UserEmail, StringComparison.OrdinalIgnoreCase))
            return Unauthorized(new ApiResponse<object>(false, null, "User mismatch"));

        using var tx = await _db.Database.BeginTransactionAsync();
        var now = DateTime.UtcNow;

        // Validate locks are held by this user and not expired
        var locks = await _db.SeatLocks
            .Where(l => l.ShowId == req.ShowId && req.SeatIds.Contains(l.SeatId) &&
                        l.ExpiresAtUtc > now && l.LockedBy == req.UserEmail)
            .ToListAsync();

        if (locks.Count != req.SeatIds.Distinct().Count())
            return Conflict(new ApiResponse<object>(false, null, "Missing or expired locks"));

        // Ensure not already booked
        var alreadyBooked = await _db.BookingSeats.AnyAsync(bs =>
            req.SeatIds.Contains(bs.SeatId) &&
            _db.Bookings.Any(b => b.Id == bs.BookingId && b.ShowId == req.ShowId && b.Status == "Confirmed"));

        if (alreadyBooked)
            return Conflict(new ApiResponse<object>(false, null, "Some seats already booked"));

        var show = await _db.Shows.FindAsync(req.ShowId);
        if (show is null) return NotFound(new ApiResponse<object>(false, null, "Show not found"));

        var total = show.Price * req.SeatIds.Distinct().Count();

        // TODO: integrate payment → only mark Confirmed after payment success
        var userId = await _db.Users.Where(u => u.Email == req.UserEmail).Select(u => u.Id).FirstOrDefaultAsync();
        var booking = new Booking { ShowId = req.ShowId, UserId = userId, Status = "Confirmed", TotalAmount = total };
        _db.Bookings.Add(booking);
        await _db.SaveChangesAsync();

        foreach (var seatId in req.SeatIds.Distinct())
            _db.BookingSeats.Add(new BookingSeat { BookingId = booking.Id, SeatId = seatId });

        _db.SeatLocks.RemoveRange(locks);
        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return Ok(new ApiResponse<object>(true, new { bookingId = booking.Id, total }));
    }

    [HttpPost("release-expired-locks")]
    public async Task<IActionResult> Cleanup()
    {
        var now = DateTime.UtcNow;
        var expired = await _db.SeatLocks.Where(l => l.ExpiresAtUtc <= now).ToListAsync();
        if (expired.Any())
        {
            _db.SeatLocks.RemoveRange(expired);
            await _db.SaveChangesAsync();
        }
        return Ok(new { removed = expired.Count });
    }

    [HttpGet("my")]
    public async Task<IActionResult> MyBookings()
    {
        var email = User.GetEmail();
        if (string.IsNullOrWhiteSpace(email))
            return Unauthorized(new ApiResponse<object>(false, null, "Not logged in"));

        var userId = await _db.Users.Where(u => u.Email == email).Select(u => u.Id).FirstOrDefaultAsync();

        var bookings = await _db.Bookings
            .Where(b => b.UserId == userId)
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

    [HttpGet("{id}")]
    public async Task<IActionResult> GetBooking(int id)
    {
        var booking = await _db.Bookings
            .Where(b => b.Id == id)
            .Include(b => b.Show).ThenInclude(s => s.Movie)
            .Include(b => b.Show).ThenInclude(s => s.Screen)
            .Include(b => b.BookingSeats).ThenInclude(bs => bs.Seat)
            .FirstOrDefaultAsync();
        if (booking is null) return NotFound(new { message = "booking not found" });
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
        return Ok(result);
    }
}
