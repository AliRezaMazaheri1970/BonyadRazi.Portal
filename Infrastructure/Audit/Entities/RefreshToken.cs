using System;

namespace BonyadRazi.Portal.Infrastructure.Audit.Entities
{
    public sealed class RefreshToken
    {
        public Guid Id { get; set; }

        // FK -> UserAccounts
        public Guid UserAccountId { get; set; }

        // NEVER store raw refresh token in DB
        public string TokenHash { get; set; } = default!;

        public DateTime CreatedUtc { get; set; }
        public DateTime ExpiresUtc { get; set; }

        public DateTime? RevokedUtc { get; set; }
        public string? RevokeReason { get; set; }

        // Rotation linking
        public Guid? ReplacedByTokenId { get; set; }

        public bool IsExpired(DateTime utcNow) => utcNow >= ExpiresUtc;
        public bool IsRevoked => RevokedUtc.HasValue;
        public bool IsActive(DateTime utcNow) => !IsRevoked && !IsExpired(utcNow);
    }
}