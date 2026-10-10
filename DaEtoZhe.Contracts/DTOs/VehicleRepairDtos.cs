using DaEtoZhe.Contracts.Enums;
using DaEtoZhe.Contracts.Models;
using System.Collections.Generic;

namespace DaEtoZhe.Contracts.DTOs
{
    /// <summary>Сводка ремонта: запчасти + работы + финансы + контроль сметы.</summary>
    public class VehicleRepairSummary
    {
        public List<VehicleRepairPart> Parts { get; set; } = new List<VehicleRepairPart>();
        public List<VehicleRepairWork> Works { get; set; } = new List<VehicleRepairWork>();
        public decimal PartsCost { get; set; }
        public decimal LaborCost { get; set; }
        public decimal TotalCost { get; set; }
        /// <summary>Смета с приёмки.</summary>
        public decimal EstimateCost { get; set; }
        /// <summary>Факт минус смета. Положительное = превышение.</summary>
        public decimal Overrun { get; set; }
        /// <summary> Сумма, которую должен получить каждый механик за работы по ремонту (с учётом долей).</summary>
        public List<VehicleMechanicShare> Shares { get; set; } = new List<VehicleMechanicShare>();
    }

    public class AddRepairPartDto
    {
        public int StockItemId { get; set; }
        public decimal Quantity { get; set; }
        public string Comment { get; set; } = string.Empty;
        public string AddedBy { get; set; } = string.Empty;
    }

    public class AddRepairWorkDto
    {
        public int MechanicId { get; set; }
        public string Description { get; set; } = string.Empty;
        public int? VehicleDefectId { get; set; }
        public decimal PlannedHours { get; set; }
        public decimal ActualHours { get; set; }
        public decimal HourlyRate { get; set; }
        public VehicleWorkStatus Status { get; set; }
        public string Comment { get; set; } = string.Empty;
    }

    public class UpdateRepairWorkDto
    {
        public int Id { get; set; }
        public int MechanicId { get; set; }
        public string Description { get; set; } = string.Empty;
        public int? VehicleDefectId { get; set; }
        public decimal PlannedHours { get; set; }
        public decimal ActualHours { get; set; }
        public decimal HourlyRate { get; set; }
        public VehicleWorkStatus Status { get; set; }
        public string Comment { get; set; } = string.Empty;

        public class AddVehicleShareDto
        {
            public int MechanicId { get; set; }
            public decimal SharePercent { get; set; }
        }

        public class UpdateVehicleShareDto
        {
            public int Id { get; set; }
            public int MechanicId { get; set; }
            public decimal SharePercent { get; set; }
        }
    }
}