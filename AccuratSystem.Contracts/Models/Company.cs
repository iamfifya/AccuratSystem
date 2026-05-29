using System;
using System.Collections.Generic;

namespace AccuratSystem.Contracts.Models
{
    public class Company
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public DateTime RegistrationDate { get; set; } = DateTime.UtcNow;
        public string Notes { get; set; } = string.Empty;

        // Навигационное свойство: у одной компании может быть много филиалов
        public List<Branch> Branches { get; set; } = new List<Branch>();
    }
}