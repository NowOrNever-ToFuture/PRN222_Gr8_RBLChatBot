namespace PRN222.Models
{
    public class UserSubscription
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public Guid PricingPackageId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int RemainingTokens { get; set; }
        public string Status { get; set; } // "Active", "Expired", "Suspended"

        // Navigation properties
        public User User { get; set; }
        public PricingPackage PricingPackage { get; set; }
    }
}
