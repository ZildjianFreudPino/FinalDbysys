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

        public IActionResult SalesHistory()
        {
            var sales = _context.Sales
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

            return View("~/Views/Dashboard/SalesHistory.cshtml", sales);
        }

    }
}