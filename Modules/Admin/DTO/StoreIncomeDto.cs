namespace AdminApi.DTOs
{
    public class StoreIncomeDto
    {
        public int StoreId { get; set; }
        public string StoreName { get; set; } // Added for better context in the response
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public decimal TotalIncome { get; set; }
        public decimal TaxedIncome { get; set; }
    }
}
