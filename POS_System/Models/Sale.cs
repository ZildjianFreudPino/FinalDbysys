namespace POS_System.Models
{
    public class Sale
    {
        public int SaleId { get; set; }
        public DateTime SaleDate { get; set; }
        public decimal SubTotal { get; set; }
        public decimal DiscountPct { get; set; }
        public decimal DiscountAmt { get; set; }
        public decimal TotalAmount { get; set; }
        public string SaleStatus { get; set; } = "Completed";
        public List<SaleItem> Items { get; set; } = new();
        public Payment? Payment { get; set; }
    }

    public class SaleItem
    {
        public int SaleItemId { get; set; }
        public int SaleId { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = "";
        public int QuantitySold { get; set; }
        public decimal PriceAtSale { get; set; }
        public decimal LineTotal => PriceAtSale * QuantitySold;
    }

    public class Payment
    {
        public int PaymentId { get; set; }
        public int SaleId { get; set; }
        public int ModeId { get; set; }
        public string ModeName { get; set; } = "";
        public decimal AmountTendered { get; set; }
        public decimal ChangeGiven { get; set; }
        public DateTime PaidAt { get; set; }
    }

    public class PaymentMode
    {
        public int ModeId { get; set; }
        public string ModeName { get; set; } = "";
    }
}