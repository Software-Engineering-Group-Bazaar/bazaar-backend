namespace Loyalty.Models
{
    public class LoyaltyRates
    {
        public static double AdminPaysSeller { get; set; } = 0.007;
        public static double SellerPaysAdmin { get; set; } = 0.01;
        public static double SpendingPointRate { get; set; } = 0.01;
    }
}