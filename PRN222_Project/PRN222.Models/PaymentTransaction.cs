namespace PRN222.Models
{
    public class PaymentTransaction
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public Guid PricingPackageId { get; set; }
        public string TransactionCode { get; set; }
        public double Amount { get; set; }
        public string PaymentMethod { get; set; } // "PayOS", "SystemFree"
        public string Status { get; set; } // "Pending", "Success", "Failed"
        public DateTime CreatedDate { get; set; }

        // Navigation properties
        public User User { get; set; }
        public PricingPackage PricingPackage { get; set; }
    }
}
