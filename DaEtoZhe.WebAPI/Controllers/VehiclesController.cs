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
    }
}