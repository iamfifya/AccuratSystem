using DaEtoZhe.Contracts.Enums;

namespace DaEtoZhe.Contracts.DTOs
{
    public class CreateVehicleDefectDto
    {
        public DefectArea Area { get; set; }
        public DefectSeverity Severity { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal EstimatedCost { get; set; }
        public decimal EstimatedHours { get; set; }
        public string Notes { get; set; } = string.Empty;
    }

    public class UpdateVehicleDefectDto
    {
        public int Id { get; set; }
        public DefectArea Area { get; set; }
        public DefectSeverity Severity { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal EstimatedCost { get; set; }
        public decimal EstimatedHours { get; set; }
        public bool IsFixed { get; set; }
        public string Notes { get; set; } = string.Empty;
    }

    public class AcceptVehicleDto
    {
        public int KeysCount { get; set; }
        public bool HasPts { get; set; }
        public bool HasSts { get; set; }
        public string DocumentsNotes { get; set; } = string.Empty;
        public string ConditionSummary { get; set; } = string.Empty;
        public string AcceptedBy { get; set; } = string.Empty;
    }
}