using System;

namespace RavenDBTestApril2019
{
    public class Stat
    {
        public DateTime TimeStamp { get; set; }
        public int CPUPercentage { get; set; }
        public long CommittedRAM_Total { get; set; }
        public long DocsInDB { get; set; }
        public decimal ImportRatePerMinute { get; set; }
        public decimal PatchingRatePerMinute { get; set; }
        public long PhysicalRAM_Total { get; set; }
        public long PhysicalRAM_Available { get; set; }
        public decimal PhysicalRAM_FreePerc { get; set; }
        public decimal PhysicalRAM_OccupiedPerc { get; set; }
        public long PatchedDocCount { get; set; }
        public long CommittedRAM_Peak { get; set; }
        public string Status { get; set; }
    }
}