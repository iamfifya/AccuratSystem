using DaEtoZhe.Contracts.Enums;
using System;

namespace DaEtoZhe.Contracts.Models
{
    /// <summary>Работа механика по ремонту автомобиля (нормо-часы).</summary>
    public class VehicleRepairWork
    {
        public int Id { get; set; }
        public int VehicleId { get; set; }
        public int MechanicId { get; set; }
        /// <summary>Какой дефект ведомости закрывает эта работа.</summary>
        public int? VehicleDefectId { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal PlannedHours { get; set; }
        public decimal ActualHours { get; set; }
        /// <summary>Ставка за нормо-час (снапшот на момент записи).</summary>
        public decimal HourlyRate { get; set; }
        public VehicleWorkStatus Status { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? DoneAt { get; set; }
        public string Comment { get; set; } = string.Empty;

        /// <summary>Стоимость работы: фактические часы (или план, если факта нет) × ставка. Учитывается только Done.</summary>
        public decimal TotalCost => (ActualHours > 0 ? ActualHours : PlannedHours) * HourlyRate;

        public string StatusName
        {
            get
            {
                switch (Status)
                {
                    case VehicleWorkStatus.Planned: return "План";
                    case VehicleWorkStatus.InProgress: return "В работе";
                    default: return "Готово";
                }
            }
        }

        public Vehicle Vehicle { get; set; }
        public User Mechanic { get; set; }
        public VehicleDefect Defect { get; set; }
    }
}