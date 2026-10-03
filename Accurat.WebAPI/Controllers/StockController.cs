using Accurat.WebAPI.Data;
using AccuratSystem.Contracts.Enums;
using AccuratSystem.Contracts.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Accurat.WebAPI.Controllers
{
    /// <summary>Складской учёт: номенклатура и категории (Этап 1).</summary>
    [Route("api/[controller]")]
    [ApiController]
    public class StockController : ControllerBase
    {
        private readonly AppDbContext _context;
        public StockController(AppDbContext context) => _context = context;

        private int CurrentCompanyId => HttpContext.Request.Headers.TryGetValue("X-Company-Id", out var id) ? int.Parse(id) : 1;

        // ═════════════ КАТЕГОРИИ ═════════════

        [HttpGet("categories")]
        public async Task<ActionResult<IEnumerable<StockCategory>>> GetCategories()
        {
            return await _context.StockCategories
                .Where(c => c.CompanyId == CurrentCompanyId)
                .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
                .ToListAsync();
        }

        [HttpPost("categories")]
        public async Task<ActionResult<StockCategory>> CreateCategory(StockCategory category)
        {
            if (string.IsNullOrWhiteSpace(category.Name)) return BadRequest("Укажите название категории");
            category.Id = 0;
            category.CompanyId = CurrentCompanyId == 0 ? 1 : CurrentCompanyId;
            if (category.SortOrder == 0)
            {
                category.SortOrder = (await _context.StockCategories
                    .Where(c => c.CompanyId == category.CompanyId)
                    .MaxAsync(c => (int?)c.SortOrder) ?? 0) + 1;
            }
            _context.StockCategories.Add(category);
            await _context.SaveChangesAsync();
            return Ok(category);
        }

        [HttpPut("categories/{id}")]
        public async Task<IActionResult> UpdateCategory(int id, StockCategory category)
        {
            if (id != category.Id) return BadRequest("ID не совпадают");
            if (string.IsNullOrWhiteSpace(category.Name)) return BadRequest("Укажите название категории");
            var existing = await _context.StockCategories.FindAsync(id);
            if (existing == null) return NotFound();
            if (CurrentCompanyId != 0 && existing.CompanyId != CurrentCompanyId) return Forbid();
            existing.Name = category.Name.Trim();
            existing.SortOrder = category.SortOrder;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("categories/{id}")]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            var existing = await _context.StockCategories.FindAsync(id);
            if (existing == null) return NotFound();
            if (CurrentCompanyId != 0 && existing.CompanyId != CurrentCompanyId) return Forbid();
            if (await _context.StockItems.AnyAsync(i => i.CategoryId == id))
                return BadRequest("Категория используется позициями номенклатуры");
            _context.StockCategories.Remove(existing);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // ═════════════ НОМЕНКЛАТУРА ═════════════

        [HttpGet("items")]
        public async Task<ActionResult<IEnumerable<StockItem>>> GetItems(
            int? categoryId = null, string search = null, bool includeInactive = false)
        {
            var query = _context.StockItems
                .Include(i => i.Category)
                .Where(i => i.CompanyId == CurrentCompanyId);

            if (!includeInactive) query = query.Where(i => i.IsActive);
            if (categoryId.HasValue) query = query.Where(i => i.CategoryId == categoryId.Value);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.ToLower();
                query = query.Where(i => i.Name.ToLower().Contains(s) || i.Article.ToLower().Contains(s));
            }
            return await query.OrderBy(i => i.Name).ToListAsync();
        }

        [HttpPost("items")]
        public async Task<ActionResult<StockItem>> CreateItem(StockItem item)
        {
            var error = ValidateItem(item);
            if (error != null) return BadRequest(error);
            item.Id = 0;
            item.CompanyId = CurrentCompanyId == 0 ? 1 : CurrentCompanyId;
            item.IsActive = true;
            item.Name = item.Name.Trim();
            item.Article = item.Article?.Trim() ?? "";
            _context.StockItems.Add(item);
            await _context.SaveChangesAsync();
            return Ok(item);
        }

        [HttpPut("items/{id}")]
        public async Task<IActionResult> UpdateItem(int id, StockItem item)
        {
            if (id != item.Id) return BadRequest("ID не совпадают");
            var error = ValidateItem(item);
            if (error != null) return BadRequest(error);
            var existing = await _context.StockItems.FindAsync(id);
            if (existing == null) return NotFound();
            if (CurrentCompanyId != 0 && existing.CompanyId != CurrentCompanyId) return Forbid();

            existing.Name = item.Name.Trim();
            existing.Article = item.Article?.Trim() ?? "";
            existing.CategoryId = item.CategoryId;
            existing.Unit = item.Unit;
            existing.PurchaseUnit = item.PurchaseUnit;
            existing.PurchaseRatio = item.PurchaseRatio;
            existing.MinStock = item.MinStock;
            existing.LastPurchaseCost = item.LastPurchaseCost;
            existing.Notes = item.Notes ?? "";
            existing.IsActive = item.IsActive;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        /// <summary>Удаление = архивация: будущие движения будут ссылаться на позицию.</summary>
        [HttpDelete("items/{id}")]
        public async Task<IActionResult> ArchiveItem(int id)
        {
            var existing = await _context.StockItems.FindAsync(id);
            if (existing == null) return NotFound();
            if (CurrentCompanyId != 0 && existing.CompanyId != CurrentCompanyId) return Forbid();
            existing.IsActive = false;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private static string ValidateItem(StockItem item)
        {
            if (string.IsNullOrWhiteSpace(item.Name)) return "Укажите название позиции";
            if (item.PurchaseRatio <= 0) return "Коэффициент приёмки должен быть больше нуля";
            if (item.MinStock < 0) return "Минимальный остаток не может быть отрицательным";
            if (item.LastPurchaseCost < 0) return "Цена закупки не может быть отрицательной";
            return null;
        }

        // ═════════════ ОСТАТКИ ═════════════

        [HttpGet("balances")]
        public async Task<ActionResult<IEnumerable<StockBalance>>> GetBalances(int branchId)
        {
            if (!await VerifyBranchAccess(branchId)) return Forbid();
            return await _context.StockBalances
                .Include(b => b.Item)
                .Where(b => b.BranchId == branchId && b.Item.CompanyId == CurrentCompanyId)
                .OrderBy(b => b.Item.Name)
                .ToListAsync();
        }

        // ═════════════ ДОКУМЕНТЫ ═════════════

        [HttpGet("documents")]
        public async Task<ActionResult<IEnumerable<StockDocument>>> GetDocuments(int branchId, StockDocumentType? type = null)
        {
            if (!await VerifyBranchAccess(branchId)) return Forbid();
            var query = _context.StockDocuments
                .Include(d => d.Movements).ThenInclude(m => m.Item)
                .Where(d => d.BranchId == branchId && d.CompanyId == CurrentCompanyId);
            if (type.HasValue) query = query.Where(d => d.Type == type.Value);
            return await query.OrderByDescending(d => d.CreatedAt).Take(200).ToListAsync();
        }

        /// <summary>
        /// Проведение документа (Приход/Списание): шапка + движения + пересчёт
        /// баланса и скользящей средней — в ОДНОЙ транзакции.
        /// </summary>
        [HttpPost("documents")]
        public async Task<ActionResult<StockDocument>> CreateDocument(StockDocument doc)
        {
            if (doc?.Movements == null || !doc.Movements.Any())
                return BadRequest("Документ без позиций");
            if (doc.Type != StockDocumentType.Receipt && doc.Type != StockDocumentType.WriteOff)
                return BadRequest("Сейчас поддерживаются только документы «Приход» и «Списание»");
            if (!await VerifyBranchAccess(doc.BranchId)) return Forbid();

            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    var header = new StockDocument
                    {
                        CompanyId = CurrentCompanyId == 0 ? 1 : CurrentCompanyId,
                        BranchId = doc.BranchId,
                        Type = doc.Type,
                        Number = "",
                        SupplierName = doc.SupplierName?.Trim() ?? "",
                        Comment = doc.Comment?.Trim() ?? "",
                        CreatedAt = DateTime.UtcNow,
                        CreatedBy = string.IsNullOrWhiteSpace(doc.CreatedBy) ? "Неизвестно" : doc.CreatedBy
                    };
                    _context.StockDocuments.Add(header);
                    await _context.SaveChangesAsync(); // получаем Id

                    header.Number = (header.Type == StockDocumentType.Receipt ? "ПР-" : "СП-") + header.Id.ToString("D5");

                    foreach (var line in doc.Movements)
                    {
                        if (line.Quantity <= 0)
                            return BadRequest("Количество в позиции должно быть больше нуля");

                        var item = await _context.StockItems.FindAsync(line.ItemId);
                        if (item == null || item.CompanyId != header.CompanyId)
                            return BadRequest($"Позиция #{line.ItemId} не найдена или принадлежит другой компании");

                        var balance = await _context.StockBalances
                            .FirstOrDefaultAsync(b => b.ItemId == item.Id && b.BranchId == header.BranchId);
                        if (balance == null)
                        {
                            balance = new StockBalance
                            {
                                ItemId = item.Id,
                                BranchId = header.BranchId,
                                Quantity = 0,
                                AvgCost = item.LastPurchaseCost
                            };
                            _context.StockBalances.Add(balance);
                            await _context.SaveChangesAsync();
                        }

                        decimal cost;
                        if (header.Type == StockDocumentType.Receipt)
                        {
                            cost = line.CostPrice;
                            if (cost < 0) return BadRequest("Цена закупки не может быть отрицательной");

                            // Скользящая средняя: newAvg = (oldQty*oldAvg + qty*cost) / newQty
                            var newQty = balance.Quantity + line.Quantity;
                            balance.AvgCost = newQty > 0
                                ? Math.Round((balance.Quantity * balance.AvgCost + line.Quantity * cost) / newQty, 2)
                                : cost;
                            balance.Quantity = newQty;
                            item.LastPurchaseCost = cost;

                            _context.StockMovements.Add(new StockMovement
                            {
                                DocumentId = header.Id,
                                ItemId = item.Id,
                                BranchId = header.BranchId,
                                Type = StockMovementType.Receipt,
                                Quantity = line.Quantity,
                                CostPrice = cost,
                                CreatedAt = DateTime.UtcNow,
                                CreatedBy = header.CreatedBy,
                                Comment = header.Comment
                            });
                        }
                        else
                        {
                            // Списание по скользящей средней; отрицательный остаток разрешён (подсветится в UI)
                            cost = balance.AvgCost;
                            balance.Quantity -= line.Quantity;

                            _context.StockMovements.Add(new StockMovement
                            {
                                DocumentId = header.Id,
                                ItemId = item.Id,
                                BranchId = header.BranchId,
                                Type = StockMovementType.WriteOff,
                                Quantity = -line.Quantity,
                                CostPrice = cost,
                                CreatedAt = DateTime.UtcNow,
                                CreatedBy = header.CreatedBy,
                                Comment = header.Comment
                            });
                        }
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    var result = await _context.StockDocuments
                        .Include(d => d.Movements).ThenInclude(m => m.Item)
                        .FirstAsync(d => d.Id == header.Id);
                    return Ok(result);
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
        }

        // ═════════════ НОРМЫ СПИСАНИЯ ═════════════

        [HttpGet("norms")]
        public async Task<ActionResult<IEnumerable<ServiceStockNorm>>> GetNorms(int serviceId)
        {
            var service = await _context.Services.FindAsync(serviceId);
            if (service == null) return NotFound();
            if (CurrentCompanyId != 0 && service.CompanyId != CurrentCompanyId) return Forbid();
            return await _context.ServiceStockNorms
                .Include(n => n.Item)
                .Where(n => n.ServiceId == serviceId)
                .OrderBy(n => n.Item.Name)
                .ToListAsync();
        }

        [HttpPost("norms")]
        public async Task<ActionResult<ServiceStockNorm>> CreateNorm(ServiceStockNorm norm)
        {
            if (norm.Quantity <= 0) return BadRequest("Норма должна быть больше нуля");
            var service = await _context.Services.FindAsync(norm.ServiceId);
            if (service == null) return NotFound("Услуга не найдена");
            if (CurrentCompanyId != 0 && service.CompanyId != CurrentCompanyId) return Forbid();
            var item = await _context.StockItems.FindAsync(norm.ItemId);
            if (item == null || item.CompanyId != service.CompanyId)
                return BadRequest("Позиция не найдена или принадлежит другой компании");
            if (await _context.ServiceStockNorms.AnyAsync(n => n.ServiceId == norm.ServiceId && n.ItemId == norm.ItemId))
                return BadRequest("Норма для этой позиции уже задана — измените существующую");

            norm.Id = 0;
            _context.ServiceStockNorms.Add(norm);
            await _context.SaveChangesAsync();
            return Ok(norm);
        }

        [HttpPut("norms/{id}")]
        public async Task<IActionResult> UpdateNorm(int id, ServiceStockNorm norm)
        {
            if (id != norm.Id) return BadRequest("ID не совпадают");
            if (norm.Quantity <= 0) return BadRequest("Норма должна быть больше нуля");
            var existing = await _context.ServiceStockNorms.FindAsync(id);
            if (existing == null) return NotFound();
            var service = await _context.Services.FindAsync(existing.ServiceId);
            if (CurrentCompanyId != 0 && service?.CompanyId != CurrentCompanyId) return Forbid();
            existing.Quantity = norm.Quantity;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("norms/{id}")]
        public async Task<IActionResult> DeleteNorm(int id)
        {
            var existing = await _context.ServiceStockNorms.FindAsync(id);
            if (existing == null) return NotFound();
            var service = await _context.Services.FindAsync(existing.ServiceId);
            if (CurrentCompanyId != 0 && service?.CompanyId != CurrentCompanyId) return Forbid();
            _context.ServiceStockNorms.Remove(existing);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // ═════════════ БЕЗОПАСНОСТЬ ═════════════

        private async Task<bool> VerifyBranchAccess(int branchId)
        {
            if (CurrentCompanyId == 0) return true;
            var branch = await _context.Branches.FindAsync(branchId);
            return branch != null && branch.CompanyId == CurrentCompanyId;
        }
    }
}