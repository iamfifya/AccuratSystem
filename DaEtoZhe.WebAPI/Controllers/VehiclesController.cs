using DaEtoZhe.Contracts.DTOs;
using DaEtoZhe.Contracts.Enums;
using DaEtoZhe.Contracts.Models;
using DaEtoZhe.WebAPI.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DaEtoZhe.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class VehiclesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public VehiclesController(AppDbContext context)
        {
            _context = context;
        }

        private int CurrentCompanyId => HttpContext.Request.Headers.TryGetValue("X-Company-Id", out var id) ? int.Parse(id) : 1;

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Vehicle>>> GetVehicles(VehicleStatus? status = null)
        {
            var query = _context.Vehicles
                .Include(v => v.Branch)
                .Where(v => v.CompanyId == CurrentCompanyId);

            if (status.HasValue)
                query = query.Where(v => v.Status == status.Value);

            return await query.OrderByDescending(v => v.CreatedAt).ToListAsync();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Vehicle>> GetVehicle(int id)
        {
            var vehicle = await _context.Vehicles
                .Include(v => v.Branch)
                .FirstOrDefaultAsync(v => v.Id == id && v.CompanyId == CurrentCompanyId);

            if (vehicle == null)
                return NotFound();

            return vehicle;
        }

        [HttpPost]
        public async Task<ActionResult<Vehicle>> CreateVehicle(CreateVehicleDto dto)
        {
            var vehicle = new Vehicle
            {
                CompanyId = dto.CompanyId > 0 ? dto.CompanyId : CurrentCompanyId,
                BranchId = dto.BranchId,
                Vin = dto.Vin,
                LicensePlate = dto.LicensePlate,
                Make = dto.Make,
                Model = dto.Model,
                Year = dto.Year,
                Mileage = dto.Mileage,
                Color = dto.Color,
                EngineType = dto.EngineType,
                EngineVolume = dto.EngineVolume,
                Transmission = dto.Transmission,
                PurchasePrice = dto.PurchasePrice,
                SellerFullName = dto.SellerFullName,
                SellerPhone = dto.SellerPhone,
                SellerPassport = dto.SellerPassport,
                Defects = dto.Defects,
                GeneralNotes = dto.GeneralNotes,
                Status = VehicleStatus.Purchase,
                PurchaseDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            _context.Vehicles.Add(vehicle);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetVehicle), new { id = vehicle.Id }, vehicle);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateVehicle(int id, UpdateVehicleDto dto)
        {
            if (id != dto.Id)
                return BadRequest();

            var vehicle = await _context.Vehicles
                .FirstOrDefaultAsync(v => v.Id == id && v.CompanyId == CurrentCompanyId);

            if (vehicle == null)
                return NotFound();

            vehicle.BranchId = dto.BranchId;
            vehicle.Status = dto.Status;
            vehicle.RepairCost = dto.RepairCost;
            vehicle.PartsCost = dto.PartsCost;
            vehicle.LaborCost = dto.LaborCost;
            vehicle.ListingPrice = dto.ListingPrice;
            vehicle.SalePrice = dto.SalePrice;
            vehicle.RepairNotes = dto.RepairNotes;
            vehicle.GeneralNotes = dto.GeneralNotes;
            vehicle.BuyerFullName = dto.BuyerFullName;
            vehicle.BuyerPhone = dto.BuyerPhone;
            vehicle.BuyerPassport = dto.BuyerPassport;

            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpPatch("{id}/status")]
        public async Task<IActionResult> ChangeStatus(int id, ChangeVehicleStatusDto dto)
        {
            var vehicle = await _context.Vehicles
                .FirstOrDefaultAsync(v => v.Id == id && v.CompanyId == CurrentCompanyId);

            if (vehicle == null)
                return NotFound();

            vehicle.Status = dto.NewStatus;

            // Обновляем даты в зависимости от статуса
            switch (dto.NewStatus)
            {
                case VehicleStatus.Appraisal:
                    vehicle.AppraisalDate = DateTime.UtcNow;
                    break;
                case VehicleStatus.Repair:
                    vehicle.RepairStartDate = DateTime.UtcNow;
                    break;
                case VehicleStatus.Prep:
                    vehicle.RepairEndDate = DateTime.UtcNow;
                    break;
                case VehicleStatus.Listed:
                    vehicle.ListedDate = DateTime.UtcNow;
                    break;
                case VehicleStatus.Sold:
                    vehicle.SoldDate = DateTime.UtcNow;
                    break;
            }

            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteVehicle(int id)
        {
            var vehicle = await _context.Vehicles
                .FirstOrDefaultAsync(v => v.Id == id && v.CompanyId == CurrentCompanyId);

            if (vehicle == null)
                return NotFound();

            _context.Vehicles.Remove(vehicle);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // ═════════════ ДЕФЕКТНАЯ ВЕДОМОСТЬ ═════════════

        [HttpGet("{id}/defects")]
        public async Task<ActionResult<IEnumerable<VehicleDefect>>> GetDefects(int id)
        {
            if (!await VehicleExistsAsync(id)) return NotFound();
            var defects = await _context.VehicleDefects
                .Where(d => d.VehicleId == id)
                .ToListAsync();
            // Сортируем в памяти: enum хранится строкой, ORDER BY по строке дал бы алфавит
            return Ok(defects
                .OrderByDescending(d => d.Severity)
                .ThenBy(d => d.Area)
                .ToList());
        }

        [HttpPost("{id}/defects")]
        public async Task<ActionResult<VehicleDefect>> CreateDefect(int id, CreateVehicleDefectDto dto)
        {
            if (!await VehicleExistsAsync(id)) return NotFound();
            if (string.IsNullOrWhiteSpace(dto.Description)) return BadRequest("Опишите дефект");

            var defect = new VehicleDefect
            {
                VehicleId = id,
                Area = dto.Area,
                Severity = dto.Severity,
                Description = dto.Description.Trim(),
                EstimatedCost = dto.EstimatedCost,
                EstimatedHours = dto.EstimatedHours,
                Notes = dto.Notes?.Trim() ?? ""
            };
            _context.VehicleDefects.Add(defect);
            await _context.SaveChangesAsync();
            return Ok(defect);
        }

        [HttpPut("defects/{defectId}")]
        public async Task<IActionResult> UpdateDefect(int defectId, UpdateVehicleDefectDto dto)
        {
            if (defectId != dto.Id) return BadRequest();
            var defect = await _context.VehicleDefects.FindAsync(defectId);
            if (defect == null) return NotFound();
            if (!await VehicleExistsAsync(defect.VehicleId)) return NotFound();

            defect.Area = dto.Area;
            defect.Severity = dto.Severity;
            defect.Description = dto.Description?.Trim() ?? "";
            defect.EstimatedCost = dto.EstimatedCost;
            defect.EstimatedHours = dto.EstimatedHours;
            defect.Notes = dto.Notes?.Trim() ?? "";
            if (dto.IsFixed && !defect.IsFixed) defect.FixedAt = DateTime.UtcNow;
            if (!dto.IsFixed) defect.FixedAt = null;
            defect.IsFixed = dto.IsFixed;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("defects/{defectId}")]
        public async Task<IActionResult> DeleteDefect(int defectId)
        {
            var defect = await _context.VehicleDefects.FindAsync(defectId);
            if (defect == null) return NotFound();
            if (!await VehicleExistsAsync(defect.VehicleId)) return NotFound();
            _context.VehicleDefects.Remove(defect);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // ═════════════ АКТ ПРИЁМКИ ═════════════

        /// <summary>
        /// Завершает приёмку: сохраняет акт, фиксирует смету по дефектам
        /// и переводит статус Purchase → Appraisal (атомарно).
        /// </summary>
        [HttpPost("{id}/acceptance")]
        public async Task<ActionResult<Vehicle>> AcceptVehicle(int id, AcceptVehicleDto dto)
        {
            var vehicle = await _context.Vehicles
                .FirstOrDefaultAsync(v => v.Id == id && v.CompanyId == CurrentCompanyId);
            if (vehicle == null) return NotFound();

            vehicle.KeysCount = dto.KeysCount;
            vehicle.HasPts = dto.HasPts;
            vehicle.HasSts = dto.HasSts;
            vehicle.DocumentsNotes = dto.DocumentsNotes?.Trim() ?? "";
            vehicle.ConditionSummary = dto.ConditionSummary?.Trim() ?? "";
            vehicle.AcceptedBy = string.IsNullOrWhiteSpace(dto.AcceptedBy) ? "Не указан" : dto.AcceptedBy.Trim();
            vehicle.AcceptedAt = DateTime.UtcNow;

            // Смета-снапшот: сумма оценочных стоимостей дефектов
            vehicle.EstimateCost = await _context.VehicleDefects
                .Where(d => d.VehicleId == id)
                .SumAsync(d => (decimal?)d.EstimatedCost) ?? 0m;

            vehicle.Status = VehicleStatus.Appraisal;
            vehicle.AppraisalDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok(vehicle);
        }

        private async Task<bool> VehicleExistsAsync(int id) =>
            await _context.Vehicles.AnyAsync(v => v.Id == id && v.CompanyId == CurrentCompanyId);
    }
}