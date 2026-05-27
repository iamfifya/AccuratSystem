using AccuratSystem.Contracts.Models;

namespace AccuratSystem.Contracts.DTOs
{
    public class LoginResponseDto
    {
        public User User { get; set; }
        public TenantFeaturesDto Features { get; set; }
    }
}
