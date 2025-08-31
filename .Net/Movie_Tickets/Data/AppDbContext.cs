using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Reflection.Emit;

namespace Movie_Tickets.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> opts) : base(opts) { }

    public DbSet<Movie> Movies => Set<Movie>();
    public DbSet<Theater> Theaters => Set<Theater>();
    public DbSet<Screen> Screens => Set<Screen>();
    public DbSet<Seat> Seats => Set<Seat>();
    public DbSet<Show> Shows => Set<Show>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<BookingSeat> BookingSeats => Set<BookingSeat>();
    public DbSet<SeatLock> SeatLocks => Set<SeatLock>();

    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // Screen → Theater (many-to-one)
        b.Entity<Screen>()
            .HasOne(s => s.Theater)
            .WithMany(t => t.Screens)
            .HasForeignKey(s => s.TheaterId);

        // Seat → Screen (many-to-one)
        b.Entity<Seat>()
            .HasOne(s => s.Screen)
            .WithMany(sc => sc.Seats)
            .HasForeignKey(s => s.ScreenId);

        // Show → Movie (many-to-one)
        b.Entity<Show>()
            .HasOne(sh => sh.Movie)
            .WithMany(m => m.Shows)
            .HasForeignKey(sh => sh.MovieId);

        // Show → Screen (many-to-one)
        b.Entity<Show>()
            .HasOne(sh => sh.Screen)
            .WithMany(sc => sc.Shows)
            .HasForeignKey(sh => sh.ScreenId);

        // BookingSeat (many-to-many between Booking & Seat)
        b.Entity<BookingSeat>()
            .HasKey(bs => new { bs.BookingId, bs.SeatId });

        b.Entity<BookingSeat>()
            .HasOne(bs => bs.Booking)
            .WithMany(bk => bk.BookingSeats)
            .HasForeignKey(bs => bs.BookingId)
            .OnDelete(DeleteBehavior.Cascade); // ✅ keep cascade here

        b.Entity<BookingSeat>()
            .HasOne(bs => bs.Seat)
            .WithMany(s => s.BookingSeats)
            .HasForeignKey(bs => bs.SeatId)
            .OnDelete(DeleteBehavior.Restrict); // ✅ prevent multiple cascade paths

        // SeatLock unique constraint (ShowId + SeatId)
        b.Entity<SeatLock>()
            .HasIndex(sl => new { sl.ShowId, sl.SeatId })
            .IsUnique();

        // Default Booking Status
        b.Entity<Booking>()
            .Property(bk => bk.Status)
            .HasDefaultValue("Pending");

        // ✅ Fix decimal precision for SQL Server
        b.Entity<Show>()
            .Property(s => s.Price)
            .HasColumnType("decimal(18,2)");

        b.Entity<Booking>()
            .Property(b => b.TotalAmount)
            .HasColumnType("decimal(18,2)");

        b.Entity<Movie>().HasData(
        new Movie { Id = 1, Title = "Inception", DurationMinutes = 148 },
        new Movie { Id = 2, Title = "The Dark Knight", DurationMinutes = 152 }
    );

        b.Entity<Theater>().HasData(
            new Theater { Id = 1, Name = "IMAX Central" }
        );

        b.Entity<Screen>().HasData(
            new Screen { Id = 1, Name = "Screen 1", TheaterId = 1 }
        );

        b.Entity<Seat>().HasData(
            new Seat { Id = 1, Number = 1, ScreenId = 1 },
            new Seat { Id = 2, Number = 1, ScreenId = 1 }
        );

        b.Entity<Show>().HasData(
            new Show { Id = 1, MovieId = 1, ScreenId = 1, StartsAtUtc = new DateTime(2025, 08, 30, 18, 00, 00) },
            new Show { Id = 2, MovieId = 2, ScreenId = 1, StartsAtUtc = new DateTime(2025, 08, 30, 18, 00, 00) }
        );
    }

}

public class Movie
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public int DurationMinutes { get; set; }
    public string? Rating { get; set; } // e.g., PG-13
    public string? Language { get; set; }
    public string? Genre { get; set; }
    public DateTime? ReleaseDate { get; set; }
    public List<Show> Shows { get; set; } = new();
}

public class Theater
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Location { get; set; } = "";
    public List<Screen> Screens { get; set; } = new();
}

public class Screen
{
    public int Id { get; set; }
    public int TheaterId { get; set; }
    public Theater Theater { get; set; } = null!;
    public string Name { get; set; } = "";
    public int TotalSeats { get; set; }
    public List<Seat> Seats { get; set; } = new();
    public List<Show> Shows { get; set; } = new();
}

public class Seat
{
    public int Id { get; set; }
    public int ScreenId { get; set; }
    public Screen Screen { get; set; } = null!;
    public string Row { get; set; } = "A";
    public int Number { get; set; } // 1..n
    public bool IsDisabled { get; set; }
    public List<BookingSeat> BookingSeats { get; set; } = new(); // <-- Add this property
}

public class Show
{
    public int Id { get; set; }
    public int MovieId { get; set; }
    public Movie Movie { get; set; } = null!;
    public int ScreenId { get; set; }
    public Screen Screen { get; set; } = null!;
    public DateTime StartsAtUtc { get; set; }
    public decimal Price { get; set; }
    public List<Booking> Bookings { get; set; } = new();
}

public class Booking
{
    public int Id { get; set; }
    public int ShowId { get; set; }
    public Show Show { get; set; } = null!;
    public string UserEmail { get; set; } = ""; // later: Identity UserId
    public string Status { get; set; } = "Pending"; // Pending/Confirmed/Cancelled
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public List<BookingSeat> BookingSeats { get; set; } = new();
}

public class BookingSeat
{
    public int BookingId { get; set; }
    public Booking Booking { get; set; } = null!;
    public int SeatId { get; set; }
    public Seat Seat { get; set; } = null!;
}

public class SeatLock
{
    public int Id { get; set; }
    public int ShowId { get; set; }
    public int SeatId { get; set; }
    public string LockedBy { get; set; } = ""; // user/email
    public DateTime ExpiresAtUtc { get; set; }
}

public class User
{
    public int Id { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "User"; // e.g., "User", "Admin"
}
// OtherInformation: Compare this snippet from Controllers/BookingsController.cs: