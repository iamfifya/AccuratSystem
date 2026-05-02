using System.ComponentModel.DataAnnotations.Schema;

namespace Accurat.WebAPI.Models
{
    public class Service
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int DurationMinutes { get; set; }
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;

        [Column(TypeName = "jsonb")]
        public Dictionary<int, decimal> PriceByBodyType { get; set; } = new Dictionary<int, decimal>();
    }
}