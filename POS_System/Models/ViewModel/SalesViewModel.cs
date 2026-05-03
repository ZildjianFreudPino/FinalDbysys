using POS_System.Models;

namespace POS_System.Models.ViewModels
{
    public class SalesViewModel
    {
        public List<Product> Products { get; set; } = new();
        public List<PaymentMode> PaymentModes { get; set; } = new();
    }

    public class ProcessSaleRequest
    {
        public List<CartItemDto> Items { get; set; } = new();
        public decimal DiscountPct { get; set; }
        public int ModeId { get; set; }
        public decimal AmountTendered { get; set; }
    }

    public class CartItemDto
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
    }

    public class SaleReceiptDto
    {
        public int SaleId { get; set; }
        public DateTime SaleDate { get; set; }
        public List<ReceiptLineDto> Lines { get; set; } = new();
        public decimal SubTotal { get; set; }
        public decimal DiscountPct { get; set; }
        public decimal DiscountAmt { get; set; }
        public decimal TotalAmount { get; set; }
        public string PaymentMode { get; set; } = "";
        public decimal AmountTendered { get; set; }
        public decimal ChangeGiven { get; set; }
    }

    public class ReceiptLineDto
    {
        public string ProductName { get; set; } = "";
        public int Qty { get; set; }
        public decimal Price { get; set; }
        public decimal LineTotal { get; set; }
    }

    // For Sales History
    public class SaleHistoryDto
    {
        public int SaleId { get; set; }
        public DateTime SaleDate { get; set; }
        public decimal SubTotal { get; set; }
        public decimal DiscountAmt { get; set; }
        public decimal TotalAmount { get; set; }
        public string SaleStatus { get; set; } = "";
        public string PaymentMode { get; set; } = "";
        public string CashierName { get; set; } = "";
    }
}