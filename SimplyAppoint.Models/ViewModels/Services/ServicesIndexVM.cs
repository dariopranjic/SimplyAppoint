using System;
using System.Collections.Generic;
using System.Text;

namespace SimplyAppoint.Models.ViewModels.Services
{
    public class ServicesIndexVM
    {
        public List<ServiceRowVM> Services { get; set; } = new();

        public int Total { get; set; }
        public int ActiveCount { get; set; }
        public int InactiveCount => Total - ActiveCount;

        public int AvgDuration { get; set; }
        public decimal AvgPrice { get; set; }

        public decimal MinPrice { get; set; }
        public decimal MaxPrice { get; set; }

        public int MinDuration { get; set; }
        public int MaxDuration { get; set; }

        public string? MostExpensiveName { get; set; }
        public decimal MostExpensivePrice { get; set; }

        public string? LongestName { get; set; }
        public int LongestDuration { get; set; }

        public int ActivePercentage { get; set; }

        public class ServiceRowVM
        {
            public int Id { get; set; }
            public string Name { get; set; } = "";
            public int Duration { get; set; }
            public decimal Price { get; set; }
            public int BufferBefore { get; set; }
            public int BufferAfter { get; set; }
            public bool Active { get; set; }
        }
    }
}
