// Common/ShowTimeHelper.cs
namespace Movie_Tickets.Common;
public static class ShowTimeHelper
{
    public static bool Overlaps(DateTime startA, DateTime endA, DateTime startB, DateTime endB)
        => startA < endB && startB < endA;
}
