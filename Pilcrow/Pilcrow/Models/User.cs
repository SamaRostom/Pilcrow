namespace Pilcrow.Models;

public class User
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public string? FullName { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DeletedAt { get; set; }
    public ICollection<Template> Templates { get; set; } = [];

}