// Common/ApiResponse.cs
namespace Movie_Tickets.Common;
public record ApiResponse<T>(bool Success, T? Data = default, string? Message = null);
public record ApiError(string Code, string Message);
