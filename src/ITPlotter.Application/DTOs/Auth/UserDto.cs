namespace ITPlotter.Application.DTOs.Auth;

public record UserDto(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string? PhotoPath);
