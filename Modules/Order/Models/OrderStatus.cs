namespace Order.Models
{
    public enum OrderStatus
    {
        Requested,
        Confirmed,
        Rejected,
        Ready,
        Sent,
        Delivered,
        Cancelled
    }
}