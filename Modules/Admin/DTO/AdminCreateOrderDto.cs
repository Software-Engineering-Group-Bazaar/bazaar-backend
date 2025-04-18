namespace AdminApi.DTOs.Order // Use the AdminApi specific namespace
{
    public class AdminCreateOrderDto
    {
        public int BuyerId { get; set; } // Admin needs to specify Buyer
        public int StoreId { get; set; }
    }
}