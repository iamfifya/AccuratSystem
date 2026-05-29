using System.Collections.Generic;

namespace AccuratSystem.Contracts.DTOs
{
    public class OrderPreviewRequestDto
    {
        public int BranchId { get; set; }
        public int WasherId { get; set; }
        public List<int> ServiceIds { get; set; } = new List<int>();
        public int BodyTypeCategory { get; set; }
        public decimal ExtraCost { get; set; }
        public decimal DiscountPercent { get; set; }
        public decimal DiscountAmount { get; set; }
        public string Notes { get; set; } = string.Empty;
    }
}