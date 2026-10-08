namespace Pilcrow.Dtos;

public record SignupRequest(string Email, string Password, string? FullName);
public record LoginRequest(string Email, string Password);
public record RefreshRequest(string RefreshToken);
public record AuthResponse(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt);