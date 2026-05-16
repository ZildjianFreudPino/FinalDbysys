using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS_System.Data;
using POS_System.Models;
using System.Linq;
using System.Collections.Generic;
namespace POS_System.Controllers

{
    [Authorize(Roles = "Admin")]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult SalesHistory(string status, DateTime? from, DateTime? to)
        {
            var salesQuery = _context.Sales.AsQueryable();

            if (!string.IsNullOrEmpty(status))
            {
                salesQuery = salesQuery.Where(s => s.SaleStatus == status);
            }

            if (from.HasValue)
            {
                salesQuery = salesQuery.Where(s => s.SaleDate >= from.Value);
            }

            if (to.HasValue)
            {
                salesQuery = salesQuery.Where(s => s.SaleDate <= to.Value.AddDays(1));
            }

            var sales = salesQuery
                .Select(s => new POS_System.Models.ViewModels.SaleHistoryDto
                {
                    SaleId = s.SaleId,
                    SaleDate = s.SaleDate,
                    SubTotal = s.SubTotal,
                    DiscountAmt = s.DiscountAmt,
                    TotalAmount = s.TotalAmount,
                    SaleStatus = s.SaleStatus,
                    PaymentMode = "Cash"
                })
                .OrderByDescending(s => s.SaleDate)
                .ToList();

            ViewBag.Status = status;
            ViewBag.From = from?.ToString("yyyy-MM-dd");
            ViewBag.To = to?.ToString("yyyy-MM-dd");

            return View("~/Views/Dashboard/SalesHistory.cshtml", sales);
        }
        public IActionResult Detail(int id)
        {
            var sale = _context.Sales
                .Where(s => s.SaleId == id)
                .Select(s => new
                {
                    saleId = s.SaleId,
                    saleDate = s.SaleDate,
                    subTotal = s.SubTotal,
                    discountPct = s.DiscountPct,
                    discountAmt = s.DiscountAmt,
                    totalAmount = s.TotalAmount,
                    saleStatus = s.SaleStatus
                })
                .FirstOrDefault();

            if (sale == null)
                return NotFound();

            return Json(sale);
        }

        [HttpPost]
        public IActionResult Void(int id)
        {
            var sale = _context.Sales.FirstOrDefault(s => s.SaleId == id);

            if (sale == null)
                return NotFound(new { error = "Sale not found." });

            sale.SaleStatus = "Voided";

            _context.SaveChanges();

            return Json(new { message = "Sale voided successfully." });
        }

    }
}