using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using POS_System.Models;
using POS_System.Models.ViewModels;

namespace POS_System.Controllers
{
   
    public class SalesController : Controller
    {
        private readonly string _conn;

        public SalesController(IConfiguration config)
        {
            _conn = config.GetConnectionString("DefaultConnection")!;
        }

        // ── POS Page ──────────────────────────────────────────────
        [Authorize(Roles = "Cashier")]
        public IActionResult Index()
        {
            var vm = new SalesViewModel
            {
                Products = GetActiveProducts(),
                PaymentModes = GetPaymentModes()
            };
            return View(vm);
        }
        [HttpPost]
        public IActionResult Process([FromBody] ProcessSaleRequest req)
        {
            if (req.Items == null || req.Items.Count == 0)
                return BadRequest(new { error = "No items in cart." });

            // Validate ModeId
            var modes = GetPaymentModes();
            var selectedMode = modes.FirstOrDefault(m => m.ModeId == req.ModeId);
            if (selectedMode == null)
                return BadRequest(new { error = "Invalid payment mode." });

            var productIds = req.Items.Select(i => i.ProductId).ToList();
            var products = GetProductsByIds(productIds);

            foreach (var item in req.Items)
            {
                var p = products.FirstOrDefault(x => x.Id == item.ProductId);
                if (p == null)
                    return BadRequest(new { error = $"Product ID {item.ProductId} not found." });
                if (p.Stock < item.Quantity)
                    return BadRequest(new { error = $"Insufficient stock for '{p.Name}'. Available: {p.Stock}" });
            }

            decimal subTotal = req.Items.Sum(i => products.First(x => x.Id == i.ProductId).Price * i.Quantity);
            decimal discountPct = Math.Max(0, Math.Min(100, req.DiscountPct));
            decimal discountAmt = Math.Round(subTotal * discountPct / 100, 2);
            decimal total = subTotal - discountAmt;

            // For cash, validate tendered amount
            decimal amountTendered = req.ModeId == 1 ? req.AmountTendered : total;
            decimal change = req.ModeId == 1 ? Math.Max(0, amountTendered - total) : 0;

            if (req.ModeId == 1 && amountTendered < total)
                return BadRequest(new { error = "Amount tendered is less than the total." });

            int saleId;
            using var con = new SqlConnection(_conn);
            con.Open();
            using var tx = con.BeginTransaction();
            try
            {
                // Insert Sale header
                var cmdSale = new SqlCommand(@"
                    INSERT INTO dbo.Sales (SubTotal, DiscountPct, DiscountAmt, TotalAmount, SaleStatus)
                    VALUES (@sub, @dpct, @damt, @total, 'Completed');
                    SELECT SCOPE_IDENTITY();", con, tx);
                cmdSale.Parameters.AddWithValue("@sub", subTotal);
                cmdSale.Parameters.AddWithValue("@dpct", discountPct);
                cmdSale.Parameters.AddWithValue("@damt", discountAmt);
                cmdSale.Parameters.AddWithValue("@total", total);
                saleId = Convert.ToInt32(cmdSale.ExecuteScalar());

                // Insert line items + deduct stock
                foreach (var item in req.Items)
                {
                    var p = products.First(x => x.Id == item.ProductId);

                    var cmdItem = new SqlCommand(@"
                        INSERT INTO dbo.SaleItems (SaleId, ProductId, QuantitySold, PriceAtSale)
                        VALUES (@sid, @pid, @qty, @price);", con, tx);
                    cmdItem.Parameters.AddWithValue("@sid", saleId);
                    cmdItem.Parameters.AddWithValue("@pid", item.ProductId);
                    cmdItem.Parameters.AddWithValue("@qty", item.Quantity);
                    cmdItem.Parameters.AddWithValue("@price", p.Price);
                    cmdItem.ExecuteNonQuery();

                    var cmdStock = new SqlCommand(@"
                        UPDATE dbo.Products SET Stock = Stock - @qty
                        WHERE Id = @pid AND Stock >= @qty;", con, tx);
                    cmdStock.Parameters.AddWithValue("@qty", item.Quantity);
                    cmdStock.Parameters.AddWithValue("@pid", item.ProductId);
                    if (cmdStock.ExecuteNonQuery() == 0)
                    {
                        tx.Rollback();
                        return BadRequest(new { error = $"Stock conflict for '{p.Name}'. Please refresh." });
                    }
                }

                // Insert Payment
                var cmdPay = new SqlCommand(@"
                    INSERT INTO dbo.Payments (SaleId, ModeId, AmountTendered, ChangeGiven)
                    VALUES (@sid, @mode, @tendered, @change);", con, tx);
                cmdPay.Parameters.AddWithValue("@sid", saleId);
                cmdPay.Parameters.AddWithValue("@mode", req.ModeId);
                cmdPay.Parameters.AddWithValue("@tendered", amountTendered);
                cmdPay.Parameters.AddWithValue("@change", change);
                cmdPay.ExecuteNonQuery();

                tx.Commit();
            }
            catch (Exception ex)
            {
                tx.Rollback();
                return StatusCode(500, new { error = "Database error: " + ex.Message });
            }

            var receipt = new SaleReceiptDto
            {
                SaleId = saleId,
                SaleDate = DateTime.Now,
                Lines = req.Items.Select(i =>
                {
                    var p = products.First(x => x.Id == i.ProductId);
                    return new ReceiptLineDto
                    {
                        ProductName = p.Name,
                        Qty = i.Quantity,
                        Price = p.Price,
                        LineTotal = p.Price * i.Quantity
                    };
                }).ToList(),
                SubTotal = subTotal,
                DiscountPct = discountPct,
                DiscountAmt = discountAmt,
                TotalAmount = total,
                PaymentMode = selectedMode.ModeName,
                AmountTendered = amountTendered,
                ChangeGiven = change
            };

            return Ok(receipt);
        }

        // ── Sales History ─────────────────────────────────────────
        public IActionResult History(string? status, DateTime? from, DateTime? to, int page = 1)
        {
            int pageSize = 15;
            var sales = GetSaleHistory(status, from, to, page, pageSize, out int totalCount);

            ViewBag.Status = status;
            ViewBag.From = from?.ToString("yyyy-MM-dd");
            ViewBag.To = to?.ToString("yyyy-MM-dd");
            ViewBag.Page = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            return View(sales);
        }

        // ── Sale Detail (for modal) ───────────────────────────────
        public IActionResult Detail(int id)
        {
            var sale = GetSaleDetail(id);
            if (sale == null) return NotFound();
            return Json(sale);
        }

        // ── Void Sale ─────────────────────────────────────────────
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public IActionResult Void(int id)
        {
            using var con = new SqlConnection(_conn);
            con.Open();
            using var tx = con.BeginTransaction();
            try
            {
                // Restore stock
                var cmdItems = new SqlCommand(
                    "SELECT ProductId, QuantitySold FROM dbo.SaleItems WHERE SaleId = @sid", con, tx);
                cmdItems.Parameters.AddWithValue("@sid", id);
                using var r = cmdItems.ExecuteReader();
                var itemsToRestore = new List<(int pid, int qty)>();
                while (r.Read())
                    itemsToRestore.Add(((int)r["ProductId"], (int)r["QuantitySold"]));
                r.Close();

                foreach (var (pid, qty) in itemsToRestore)
                {
                    var cmdStock = new SqlCommand(
                        "UPDATE dbo.Products SET Stock = Stock + @qty WHERE Id = @pid", con, tx);
                    cmdStock.Parameters.AddWithValue("@qty", qty);
                    cmdStock.Parameters.AddWithValue("@pid", pid);
                    cmdStock.ExecuteNonQuery();
                }

                // Mark as voided
                var cmdVoid = new SqlCommand(
                    "UPDATE dbo.Sales SET SaleStatus = 'Voided' WHERE SaleId = @sid AND SaleStatus = 'Completed'",
                    con, tx);
                cmdVoid.Parameters.AddWithValue("@sid", id);
                int rows = cmdVoid.ExecuteNonQuery();
                if (rows == 0)
                {
                    tx.Rollback();
                    return BadRequest(new { error = "Sale not found or already voided." });
                }

                tx.Commit();
                return Ok(new { message = $"Sale #{id} voided successfully." });
            }
            catch (Exception ex)
            {
                tx.Rollback();
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // ── Private Helpers ───────────────────────────────────────
        private List<Product> GetActiveProducts()
        {
            var list = new List<Product>();
            using var con = new SqlConnection(_conn);
            con.Open();
            using var cmd = new SqlCommand(
                "SELECT Id, Name, Description, Price, Stock, CategoryId, ImagePath FROM dbo.Products WHERE Stock > 0 ORDER BY Name", con);
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(MapProduct(r));
            return list;
        }

        private List<Product> GetProductsByIds(List<int> ids)
        {
            var list = new List<Product>();
            if (ids.Count == 0) return list;
            var inClause = string.Join(",", ids.Select((_, i) => $"@id{i}"));
            using var con = new SqlConnection(_conn);
            con.Open();
            using var cmd = new SqlCommand(
                $"SELECT Id, Name, Description, Price, Stock, CategoryId, ImagePath FROM dbo.Products WHERE Id IN ({inClause})", con);
            for (int i = 0; i < ids.Count; i++)
                cmd.Parameters.AddWithValue($"@id{i}", ids[i]);
            using var r = cmd.ExecuteReader();
            while (r.Read()) list.Add(MapProduct(r));
            return list;
        }

        private List<PaymentMode> GetPaymentModes()
        {
            var list = new List<PaymentMode>();
            using var con = new SqlConnection(_conn);
            con.Open();
            using var cmd = new SqlCommand(
                "SELECT ModeId, ModeName FROM dbo.PaymentModes WHERE IsActive=1 ORDER BY ModeId", con);
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add(new PaymentMode { ModeId = (byte)r["ModeId"], ModeName = r["ModeName"].ToString()! });
            return list;
        }

        private List<SaleHistoryDto> GetSaleHistory(
            string? status, DateTime? from, DateTime? to,
            int page, int pageSize, out int totalCount)
        {
            var where = new List<string>();
            if (!string.IsNullOrEmpty(status)) where.Add("s.SaleStatus = @status");
            if (from.HasValue) where.Add("s.SaleDate >= @from");
            if (to.HasValue) where.Add("s.SaleDate <  DATEADD(day,1,@to)");
            string filter = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "";

            var list = new List<SaleHistoryDto>();
            using var con = new SqlConnection(_conn);
            con.Open();

            // Total count
            using var cmdCount = new SqlCommand(
                $"SELECT COUNT(*) FROM dbo.Sales s {filter}", con);
            if (!string.IsNullOrEmpty(status)) cmdCount.Parameters.AddWithValue("@status", status);
            if (from.HasValue) cmdCount.Parameters.AddWithValue("@from", from.Value.Date);
            if (to.HasValue) cmdCount.Parameters.AddWithValue("@to", to.Value.Date);
            totalCount = (int)cmdCount.ExecuteScalar();

            // Paged results
            using var cmd = new SqlCommand($@"
                SELECT s.SaleId, s.SaleDate, s.SubTotal, s.DiscountAmt,
                       s.TotalAmount, s.SaleStatus, pm.ModeName
                FROM dbo.Sales s
                LEFT JOIN dbo.Payments p  ON p.SaleId = s.SaleId
                LEFT JOIN dbo.PaymentModes pm ON pm.ModeId = p.ModeId
                {filter}
                ORDER BY s.SaleDate DESC
                OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY;", con);
            if (!string.IsNullOrEmpty(status)) cmd.Parameters.AddWithValue("@status", status);
            if (from.HasValue) cmd.Parameters.AddWithValue("@from", from.Value.Date);
            if (to.HasValue) cmd.Parameters.AddWithValue("@to", to.Value.Date);
            cmd.Parameters.AddWithValue("@skip", (page - 1) * pageSize);
            cmd.Parameters.AddWithValue("@take", pageSize);

            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add(new SaleHistoryDto
                {
                    SaleId = (int)r["SaleId"],
                    SaleDate = (DateTime)r["SaleDate"],
                    SubTotal = (decimal)r["SubTotal"],
                    DiscountAmt = (decimal)r["DiscountAmt"],
                    TotalAmount = (decimal)r["TotalAmount"],
                    SaleStatus = r["SaleStatus"].ToString()!,
                    PaymentMode = r["ModeName"]?.ToString() ?? ""
                });
            return list;
        }

        private SaleReceiptDto? GetSaleDetail(int saleId)
        {
            using var con = new SqlConnection(_conn);
            con.Open();

            using var cmdSale = new SqlCommand(@"
                SELECT s.SaleId, s.SaleDate, s.SubTotal, s.DiscountPct, s.DiscountAmt,
                       s.TotalAmount, s.SaleStatus, pm.ModeName,
                       p.AmountTendered, p.ChangeGiven
                FROM dbo.Sales s
                LEFT JOIN dbo.Payments p   ON p.SaleId = s.SaleId
                LEFT JOIN dbo.PaymentModes pm ON pm.ModeId = p.ModeId
                WHERE s.SaleId = @sid", con);
            cmdSale.Parameters.AddWithValue("@sid", saleId);

            SaleReceiptDto? dto = null;
            using var r = cmdSale.ExecuteReader();
            if (r.Read())
            {
                dto = new SaleReceiptDto
                {
                    SaleId = (int)r["SaleId"],
                    SaleDate = (DateTime)r["SaleDate"],
                    SubTotal = (decimal)r["SubTotal"],
                    DiscountPct = (decimal)r["DiscountPct"],
                    DiscountAmt = (decimal)r["DiscountAmt"],
                    TotalAmount = (decimal)r["TotalAmount"],
                    PaymentMode = r["ModeName"]?.ToString() ?? "",
                    AmountTendered = r["AmountTendered"] == DBNull.Value ? 0 : (decimal)r["AmountTendered"],
                    ChangeGiven = r["ChangeGiven"] == DBNull.Value ? 0 : (decimal)r["ChangeGiven"]
                };
            }
            r.Close();
            if (dto == null) return null;

            using var cmdItems = new SqlCommand(@"
                SELECT pr.Name, si.QuantitySold, si.PriceAtSale,
                       si.PriceAtSale * si.QuantitySold AS LineTotal
                FROM dbo.SaleItems si
                JOIN dbo.Products pr ON pr.Id = si.ProductId
                WHERE si.SaleId = @sid", con);
            cmdItems.Parameters.AddWithValue("@sid", saleId);
            using var ri = cmdItems.ExecuteReader();
            dto.Lines = new List<ReceiptLineDto>();
            while (ri.Read())
                dto.Lines.Add(new ReceiptLineDto
                {
                    ProductName = ri["Name"].ToString()!,
                    Qty = (int)ri["QuantitySold"],
                    Price = (decimal)ri["PriceAtSale"],
                    LineTotal = (decimal)ri["LineTotal"]
                });
            return dto;
        }

        private static Product MapProduct(SqlDataReader r) => new()
        {
            Id = (int)r["Id"],
            Name = r["Name"].ToString()!,
            Description = r["Description"]?.ToString(),
            Price = (decimal)r["Price"],
            Stock = (int)r["Stock"],
            CategoryId = (int)r["CategoryId"],
            ImagePath = r["ImagePath"]?.ToString()
        };
    }
}