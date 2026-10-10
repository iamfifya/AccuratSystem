using DaEtoZhe.Contracts.DTOs;
using DaEtoZhe.Contracts.Enums;
using DaEtoZhe.Contracts.Models;
using DaEtoZhe.WebAPI.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace DaEtoZhe.WebAPI.Controllers
{
    [Route("api/vehicles")]
    [ApiController]
    public class VehicleRepairController : ControllerBase
    {
        private readonly AppDbContext _context;
        public VehicleRepairController(AppDbContext context) => _context = context;

        private int CurrentCompanyId => HttpContext.Request.Headers.TryGetValue("X-Company-Id", out var id) ? int.Parse(id) : 1;

        // ═════════════ СВОДКА РЕМОНТА ═════════════

        [HttpGet("{id}/repair")]
        public async Task<ActionResult<VehicleRepairSummary>> GetRepair(int id)
        {
            var vehicle = await GetVehicleAsync(id);
            if (vehicle == null) return NotFound();

            var parts = await _context.VehicleRepairParts
                .Include(p => p.StockItem)
                .Where(p => p.VehicleId == id)
                .OrderByDescending(p => p.AddedAt)
                .ToListAsync();

            var works = await _context.VehicleRepairWorks
                .Include(w => w.Mechanic)
                .Include(w => w.Defect)
                .Where(w => w.VehicleId == id)
                .OrderBy(w => w.Id)
                .ToListAsync();

            return Ok(BuildSummary(vehicle, parts, works));
        }

        // ═════════════ ЗАПЧАСТИ ═════════════

        [HttpPost("{id}/repair/parts")]
        public async Task<ActionResult<VehicleRepairPart>> AddPart(int id, AddRepairPartDto dto)
        {
            var vehicle = await GetVehicleAsync(id);
            if (vehicle == null) return NotFound();
            if (vehicle.BranchId == null)
                return BadRequest("У автомобиля не указан филиал — списание со склада невозможно");
            if (dto.Quantity <= 0)
                return BadRequest("Количество должно быть больше нуля");

            var item = await _context.StockItems
                .FirstOrDefaultAsync(i => i.Id == dto.StockItemId && i.CompanyId == CurrentCompanyId);
            if (item == null) return NotFound("Позиция номенклатуры не найдена");

            var branchId = vehicle.BranchId.Value;
            var balance = await _context.StockBalances
                .FirstOrDefaultAsync(b => b.ItemId == item.Id && b.BranchId == branchId);
            if (balance == null)
            {
                balance = new StockBalance
                {
                    ItemId = item.Id,
                    BranchId = branchId,
                    Quantity = 0,
                    AvgCost = item.LastPurchaseCost
                };
                _context.StockBalances.Add(balance);
            }

            // Себестоимость = скользящая средняя баланса (как в складских документах)
            var cost = balance.AvgCost;
            balance.Quantity -= dto.Quantity;   // отрицательный остаток разрешён политикой

            var movement = new StockMovement
            {
                ItemId = item.Id,
                BranchId = branchId,
                Type = StockMovementType.WriteOff,
                Quantity = -dto.Quantity,
                CostPrice = cost,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = string.IsNullOrWhiteSpace(dto.AddedBy) ? "Ремонт" : dto.AddedBy,
                Comment = $"Ремонт авто #{id} ({vehicle.LicensePlate})"
            };
            _context.StockMovements.Add(movement);

            var part = new VehicleRepairPart
            {
                VehicleId = id,
                StockItemId = item.Id,
                BranchId = branchId,
                Quantity = dto.Quantity,
                CostPrice = cost,
                StockMovement = movement,
                AddedAt = DateTime.UtcNow,
                AddedBy = string.IsNullOrWhiteSpace(dto.AddedBy) ? "Ремонт" : dto.AddedBy,
                Comment = dto.Comment?.Trim() ?? ""
            };
            _context.VehicleRepairParts.Add(part);

            EnsureRepairStarted(vehicle);
            await _context.SaveChangesAsync();
            await RecalcCostsAsync(vehicle);

            return Ok(part);
        }

        /// <summary>Удаление запчасти = возврат на склад через Storno (журнал движений не рвётся).</summary>
        [HttpDelete("repair/parts/{partId}")]
        public async Task<IActionResult> DeletePart(int partId)
        {
            var part = await _context.VehicleRepairParts
                .Include(p => p.Vehicle)
                .FirstOrDefaultAsync(p => p.Id == partId);
            if (part == null || part.Vehicle.CompanyId != CurrentCompanyId) return NotFound();

            var balance = await _context.StockBalances
                .FirstOrDefaultAsync(b => b.ItemId == part.StockItemId && b.BranchId == part.BranchId);
            if (balance != null) balance.Quantity += part.Quantity;

            _context.StockMovements.Add(new StockMovement
            {
                ItemId = part.StockItemId,
                BranchId = part.BranchId,
                Type = StockMovementType.Storno,
                Quantity = part.Quantity,
                CostPrice = part.CostPrice,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "Ремонт",
                Comment = $"Возврат запчасти по ремонту авто #{part.VehicleId}"
            });

            _context.VehicleRepairParts.Remove(part);
            await _context.SaveChangesAsync();
            await RecalcCostsAsync(part.Vehicle);
            return NoContent();
        }

        // ═════════════ РАБОТЫ ═════════════

        [HttpPost("{id}/repair/works")]
        public async Task<ActionResult<VehicleRepairWork>> AddWork(int id, AddRepairWorkDto dto)
        {
            var vehicle = await GetVehicleAsync(id);
            if (vehicle == null) return NotFound();

            var error = await ValidateWorkAsync(dto.MechanicId, dto.VehicleDefectId, id, dto.PlannedHours, dto.HourlyRate);
            if (error != null) return BadRequest(error);

            var work = new VehicleRepairWork
            {
                VehicleId = id,
                MechanicId = dto.MechanicId,
                VehicleDefectId = dto.VehicleDefectId,
                Description = dto.Description?.Trim() ?? "",
                PlannedHours = dto.PlannedHours,
                ActualHours = dto.ActualHours,
                HourlyRate = dto.HourlyRate,
                Status = dto.Status,
                StartedAt = dto.Status == VehicleWorkStatus.InProgress ? DateTime.UtcNow : null,
                DoneAt = dto.Status == VehicleWorkStatus.Done ? DateTime.UtcNow : null,
                Comment = dto.Comment?.Trim() ?? ""
            };
            _context.VehicleRepairWorks.Add(work);

            if (work.Status == VehicleWorkStatus.Done && work.VehicleDefectId.HasValue)
                await SetDefectFixedAsync(work.VehicleDefectId.Value, true);

            EnsureRepairStarted(vehicle);
            await _context.SaveChangesAsync();
            await RecalcCostsAsync(vehicle);
            return Ok(work);
        }

        [HttpPut("repair/works/{workId}")]
        public async Task<IActionResult> UpdateWork(int workId, UpdateRepairWorkDto dto)
        {
            if (workId != dto.Id) return BadRequest();
            var work = await _context.VehicleRepairWorks
                .Include(w => w.Vehicle)
                .FirstOrDefaultAsync(w => w.Id == workId);
            if (work == null || work.Vehicle.CompanyId != CurrentCompanyId) return NotFound();

            var error = await ValidateWorkAsync(dto.MechanicId, dto.VehicleDefectId, work.VehicleId, dto.PlannedHours, dto.HourlyRate);
            if (error != null) return BadRequest(error);

            var wasDone = work.Status == VehicleWorkStatus.Done;
            var nowDone = dto.Status == VehicleWorkStatus.Done;

            work.MechanicId = dto.MechanicId;
            work.VehicleDefectId = dto.VehicleDefectId;
            work.Description = dto.Description?.Trim() ?? "";
            work.PlannedHours = dto.PlannedHours;
            work.ActualHours = dto.ActualHours;
            work.HourlyRate = dto.HourlyRate;
            work.Status = dto.Status;
            work.Comment = dto.Comment?.Trim() ?? "";

            if (!wasDone && nowDone) work.DoneAt = DateTime.UtcNow;
            if (wasDone && !nowDone) work.DoneAt = null;
            if (work.StartedAt == null && dto.Status == VehicleWorkStatus.InProgress) work.StartedAt = DateTime.UtcNow;

            // Синхронизация дефекта: снял с Done — верни дефект в работу
            if (wasDone && !nowDone && work.VehicleDefectId.HasValue)
                await SetDefectFixedAsync(work.VehicleDefectId.Value, false);
            if (nowDone && work.VehicleDefectId.HasValue)
                await SetDefectFixedAsync(work.VehicleDefectId.Value, true);

            await _context.SaveChangesAsync();
            await RecalcCostsAsync(work.Vehicle);
            return NoContent();
        }

        [HttpDelete("repair/works/{workId}")]
        public async Task<IActionResult> DeleteWork(int workId)
        {
            var work = await _context.VehicleRepairWorks
                .Include(w => w.Vehicle)
                .FirstOrDefaultAsync(w => w.Id == workId);
            if (work == null || work.Vehicle.CompanyId != CurrentCompanyId) return NotFound();

            if (work.Status == VehicleWorkStatus.Done && work.VehicleDefectId.HasValue)
                await SetDefectFixedAsync(work.VehicleDefectId.Value, false);

            _context.VehicleRepairWorks.Remove(work);
            await _context.SaveChangesAsync();
            await RecalcCostsAsync(work.Vehicle);
            return NoContent();
        }

        // ═════════════ ЗАВЕРШЕНИЕ РЕМОНТА ═════════════

        [HttpPost("{id}/repair/finish")]
        public async Task<ActionResult<Vehicle>> FinishRepair(int id)
        {
            var vehicle = await GetVehicleAsync(id);
            if (vehicle == null) return NotFound();
            if (vehicle.Status != VehicleStatus.Repair)
                return BadRequest("Завершить можно только ремонт со статусом «В ремонте»");

            await RecalcCostsAsync(vehicle);
            vehicle.Status = VehicleStatus.Prep;
            vehicle.RepairEndDate = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Ok(vehicle);
        }

        // ═════════════ ХЕЛПЕРЫ ═════════════

        private async Task<Vehicle> GetVehicleAsync(int id) =>
            await _context.Vehicles.FirstOrDefaultAsync(v => v.Id == id && v.CompanyId == CurrentCompanyId);

        /// <summary>Первая запчасть/работа автоматически открывает ремонт.</summary>
        private static void EnsureRepairStarted(Vehicle vehicle)
        {
            if (vehicle.Status == VehicleStatus.Purchase || vehicle.Status == VehicleStatus.Appraisal)
            {
                vehicle.Status = VehicleStatus.Repair;
                vehicle.RepairStartDate = DateTime.UtcNow;
            }
        }

        private async Task<string> ValidateWorkAsync(int mechanicId, int? defectId, int vehicleId, decimal plannedHours, decimal hourlyRate)
        {
            var mechanic = await _context.Users.FirstOrDefaultAsync(u => u.Id == mechanicId && u.CompanyId == CurrentCompanyId);
            if (mechanic == null) return "Механик не найден в вашей компании";
            if (plannedHours < 0) return "Нормо-часы не могут быть отрицательными";
            if (hourlyRate < 0) return "Ставка не может быть отрицательной";
            if (defectId.HasValue)
            {
                var defect = await _context.VehicleDefects
                    .FirstOrDefaultAsync(d => d.Id == defectId.Value && d.VehicleId == vehicleId);
                if (defect == null) return "Дефект не принадлежит этому автомобилю";
            }
            return null;
        }

        private async Task SetDefectFixedAsync(int defectId, bool isFixed)
        {
            var defect = await _context.VehicleDefects.FindAsync(defectId);
            if (defect == null) return;
            defect.IsFixed = isFixed;
            defect.FixedAt = isFixed ? DateTime.UtcNow : null;
        }

        /// <summary>Пересчёт финиша ремонта: запчасти + выполненные работы → Vehicle.</summary>
        private async Task RecalcCostsAsync(Vehicle vehicle)
        {
            var partsCost = await _context.VehicleRepairParts
                .Where(p => p.VehicleId == vehicle.Id)
                .SumAsync(p => (decimal?)p.Quantity * p.CostPrice) ?? 0m;

            var works = await _context.VehicleRepairWorks
                .Where(w => w.VehicleId == vehicle.Id && w.Status == VehicleWorkStatus.Done)
                .ToListAsync();

            var laborCost = works.Sum(w => w.TotalCost);

            vehicle.PartsCost = partsCost;
            vehicle.LaborCost = laborCost;
            vehicle.RepairCost = partsCost + laborCost;
            await _context.SaveChangesAsync();
        }

        private static VehicleRepairSummary BuildSummary(
            Vehicle vehicle,
            System.Collections.Generic.List<VehicleRepairPart> parts,
            System.Collections.Generic.List<VehicleRepairWork> works)
        {
            var partsCost = parts.Sum(p => p.TotalCost);
            var laborCost = works.Where(w => w.Status == VehicleWorkStatus.Done).Sum(w => w.TotalCost);
            var total = partsCost + laborCost;

            return new VehicleRepairSummary
            {
                Parts = parts,
                Works = works,
                PartsCost = partsCost,
                LaborCost = laborCost,
                TotalCost = total,
                EstimateCost = vehicle.EstimateCost,
                Overrun = total - vehicle.EstimateCost
            };
        }
    }
}