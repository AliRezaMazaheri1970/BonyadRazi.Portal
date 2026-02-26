using System.ComponentModel.DataAnnotations;

namespace BonyadRazi.Portal.Infrastructure.Auth.Entities;

public sealed class UserAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required, MaxLength(100)]
    public string Username { get; set; } = default!;

    [Required]
    public byte[] PasswordHash { get; set; } = default!;

    [Required]
    public byte[] PasswordSalt { get; set; } = default!;

    public int PasswordIterations { get; set; } = 100_000;

    public int FailedLoginCount { get; set; } = 0;

    public DateTime? LockoutEndUtc { get; set; }

    [MaxLength(200)]
    public string Roles { get; set; } = "User";

    public Guid? CompanyCode { get; set; }

    public bool IsActive { get; set; } = true;
}