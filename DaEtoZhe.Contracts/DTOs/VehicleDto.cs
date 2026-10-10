using DaEtoZhe.Contracts.Enums;

namespace DaEtoZhe.Contracts.DTOs
{
    public class CreateVehicleDto
    {
        public int CompanyId { get; set; }
        public int? BranchId { get; set; }
        public string Vin { get; set; } = string.Empty;
        public string LicensePlate { get; set; } = string.Empty;
        public string Make { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public int Year { get; set; }
        public int Mileage { get; set; }
        public string Color { get; set; } = string.Empty;
        public string EngineType { get; set; } = string.Empty;
        public decimal EngineVolume { get; set; }
        public string Transmission { get; set; } = string.Empty;
        public decimal PurchasePrice { get; set; }
        public string SellerFullName { get; set; } = string.Empty;
        public string SellerPhone { get; set; } = string.Empty;
        public string SellerPassport { get; set; } = string.Empty;
        public string Defects { get; set; } = string.Empty;
        public string GeneralNotes { get; set; } = string.Empty;
    }

    public class UpdateVehicleDto
    {
        public int Id { get; set; }
        public int? BranchId { get; set; }
        public VehicleStatus Status { get; set; }
        public decimal RepairCost { get; set; }
        public decimal PartsCost { get; set; }
        public decimal LaborCost { get; set; }
        public decimal ListingPrice { get; set; }
        public decimal SalePrice { get; set; }
        public string RepairNotes { get; set; } = string.Empty;
        public string GeneralNotes { get; set; } = string.Empty;
        public string BuyerFullName { get; set; } = string.Empty;
        public string BuyerPhone { get; set; } = string.Empty;
        public string BuyerPassport { get; set; } = string.Empty;
    }

    public class ChangeVehicleStatusDto
    {
        public int VehicleId { get; set; }
        public VehicleStatus NewStatus { get; set; }
        public string Notes { get; set; } = string.Empty;
    }
}