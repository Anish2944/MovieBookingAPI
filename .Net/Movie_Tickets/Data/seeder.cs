using Microsoft.EntityFrameworkCore;
using Movie_Tickets.Data;

namespace MovieBooking.Api.Data;

public static class Seeder
{
    public static async Task SeedAsync(AppDbContext db)
    {
        if (!await db.Movies.AnyAsync())
        {
            var m = new Movie { Title = "Interstellar", DurationMinutes = 169, Genre = "Sci-Fi", Language = "English" };
            var t = new Theater { Name = "Galaxy Cinema", Location = "Downtown" };
            var s = new Screen { Name = "Screen 1", Theater = t, TotalSeats = 30 };
            // simple 3 rows x 10 seats
            for (var r = 0; r < 3; r++)
                for (var n = 1; n <= 10; n++)
                    s.Seats.Add(new Seat { Row = ((char)('A' + r)).ToString(), Number = n });

            var show = new Show { Movie = m, Screen = s, StartsAtUtc = DateTime.UtcNow.AddDays(1), Price = 250 };

            db.AddRange(m, t, s, show);
            await db.SaveChangesAsync();
        }
    }
}
