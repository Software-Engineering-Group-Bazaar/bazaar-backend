using System.ComponentModel.DataAnnotations;

namespace Catalog.Models
{
    public class Store
    {
        public int Id { get; set; }
        
        public required string SellerUserId { get; set; }

       
    }
}
