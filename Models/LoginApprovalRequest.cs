namespace SecureLoginApi.Pg.Models
{
    public class LoginApprovalRequest
    {
        public Guid Id { get; set; }
        public int UserId { get; set; }
        public User User { get; set; }
        public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddMinutes(5);
        public bool IsApprovedByPerson1 { get; set; } = false;
        public bool IsApprovedByPerson2 { get; set; } = false;
    }

    public enum ApprovalStatus
    {
        Pending,
        Approved,
        Rejected,
        Expired
    }
}
