using System.ComponentModel.DataAnnotations;

namespace Akkurat.WebAPI.Models
{
    public class TenantFeature
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public int BranchId { get; set; }
        public bool IsUpsellEnabled { get; set; } = false;
        public bool IsStorageEnabled { get; set; } = false;
        public bool IsCrmMarketingEnabled { get; set; } = false;
        public bool IsTelegramBossEnabled { get; set; } = false;
    }
}
