using System;

namespace DaEtoZhe.Contracts.Models
{
    /// <summary>Доля механика в зарплате за ремонт конкретной машины.</summary>
    public class VehicleMechanicShare
    {
        public int Id { get; set; }
        public int VehicleId { get; set; }
        public int MechanicId { get; set; }
        /// <summary>Процент зарплатного пула машины (сумма по машине = 100).</summary>
        public decimal SharePercent { get; set; }
        /// <summary>Снапшот начисленной суммы после проведения расчёта.</summary>
        public decimal EarnedAmount { get; set; }
        public DateTime? AccruedAt { get; set; }

        public Vehicle Vehicle { get; set; }
        public User Mechanic { get; set; }
    }
}