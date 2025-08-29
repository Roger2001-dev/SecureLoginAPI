namespace SecureLoginApi.Pg.DTOs
{
    public class MfaVerifyDto
    {
        public required string Username { get; set; }
        public required string Code { get; set; }
    }
}
