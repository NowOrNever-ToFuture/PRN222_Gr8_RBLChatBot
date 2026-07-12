using System;
using System.Collections.Generic;

namespace PRN222.Models
{
    public class PricingPackage
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public double Price { get; set; }
        public int TokenQuota { get; set; }
        public int DurationDays { get; set; }
        public bool IsActive { get; set; }
        public int MaxUploadSizeMb { get; set; }

        // Navigation properties
        public ICollection<UserSubscription> UserSubscriptions { get; set; } = new List<UserSubscription>();
    }
}
