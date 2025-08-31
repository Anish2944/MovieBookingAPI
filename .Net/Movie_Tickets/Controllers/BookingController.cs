using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Movie_Tickets.Data;

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
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(5);

        foreach (var seatId in req.SeatIds.Distinct())
        {
            _db.SeatLocks.Add(new SeatLock
            {
                ShowId = req.ShowId,
                SeatId = seatId,
                LockedBy = req.UserEmail,
                ExpiresAtUtc = expires
            });
        }

        try
        {
            await _db.SaveChangesAsync();
            return Ok(new { message = "locked", expiresAtUtc = expires });
        }
        catch (DbUpdateException)
        {
            // Unique index violation → someone else locked one of the seats
            return Conflict(new { message = "some seats already locked" });
        }
    }

    [HttpPost("confirm")]
    public async Task<IActionResult> Confirm(ConfirmRequest req)
    {
        using var tx = await _db.Database.BeginTransactionAsync();

        var now = DateTime.UtcNow;

        // Ensure all seats are locked by this user and not expired
        var locks = await _db.SeatLocks
            .Where(l => l.ShowId == req.ShowId && req.SeatIds.Contains(l.SeatId) && l.ExpiresAtUtc > now && l.LockedBy == req.UserEmail)
            .ToListAsync();

        if (locks.Count != req.SeatIds.Distinct().Count())
            return Conflict(new { message = "missing or expired locks" });

        // Ensure no confirmed booking exists for any seat
        var alreadyBooked = await _db.BookingSeats.AnyAsync(bs =>
            req.SeatIds.Contains(bs.SeatId) &&
            _db.Bookings.Any(b => b.Id == bs.BookingId && b.ShowId == req.ShowId && b.Status == "Confirmed"));

        if (alreadyBooked)
            return Conflict(new { message = "some seats already booked" });

        var show = await _db.Shows.FindAsync(req.ShowId);
        if (show is null) return NotFound(new { message = "show not found" });

        var total = show.Price * req.SeatIds.Length;

        var booking = new Booking
        {
            ShowId = req.ShowId,
            UserEmail = req.UserEmail,
            Status = "Confirmed",
            TotalAmount = total
        };

        _db.Bookings.Add(booking);
        await _db.SaveChangesAsync();

        foreach (var seatId in req.SeatIds.Distinct())
            _db.BookingSeats.Add(new BookingSeat { BookingId = booking.Id, SeatId = seatId });

        // remove the locks
        _db.SeatLocks.RemoveRange(locks);

        await _db.SaveChangesAsync();
        await tx.CommitAsync();

        return Ok(new { bookingId = booking.Id, total });
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
    public async Task<IActionResult> MyBookings([FromQuery] string userEmail)
    {
        var bookings = await _db.Bookings
            .Where(b => b.UserEmail == userEmail)
            .Include(b => b.Show).ThenInclude(s => s.Movie)
            .Include(b => b.Show).ThenInclude(s => s.Screen)
            .Include(b => b.BookingSeats).ThenInclude(bs => bs.Seat)
            .OrderByDescending(b => b.CreatedAtUtc)
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
        return Ok(result);
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
