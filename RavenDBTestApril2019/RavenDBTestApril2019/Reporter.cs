using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using Raven.Client.Documents;
using Raven.Client.Documents.Indexes;
using Raven.Client.Documents.Operations;

namespace RavenDBTestApril2019
{
    public class Reporter: IDisposable
    {
        public Reporter(IDocumentStore store)
        {
            var now = $"{DateTime.Now:O}"
                .Replace(' ', '_')
                .Replace('.', '_')
                .Replace(':', '_');
            this.Id = $"Reporter-{now}";
            this.Store = store;
            this.DatabaseId = store.Database;
            this.RavenURL = store.Urls.First();

            Stats = new List<Stat>();

            InitialiseCPUCounter();

            StartTimer();
            this.WaitForIndexing();
        }

        #region Fields and Properties
        [JsonIgnore]
        public IDocumentStore Store { get; set; }
        private readonly Stopwatch stopWatchTotalTime = new Stopwatch();

        public string Id { get; set; }
        public string ExceptionMessage { get; set; }
        public string StackTrace { get; set; }
        public DateTime LastUpdated { get; set; }
        public bool IsWaitingForIndexingToComplete { get; set; }

        public string DatabaseId { get; set; }
        public string RavenURL { get; set; }
        
        private IEnumerable<IndexInformation> indexes { get; set; }
        private List<IndexInformation> staleIndexes { get; set; }
        private string Status { get; set; }
        public long CountOfDocumentsInDB { get; set; }
        public int CountOfIndexes { get; set; }
        public DateTime LastDatabaseRefresh { get; set; }

        public DateTime? ImportStartDate { get; set; }
        public DateTime? ImportEndDate { get; set; }
        public long CountOfDocsImported { get; set; }
        public decimal ImportRatePerMinute { get; set; }

        public DateTime? IndexesCreationStartDate { get; set; }
        public DateTime? IndexesCreationEndDate { get; set; }
        public decimal IndexCreationMins { get; set; }

        public DateTime? PatchingStartDate { get; set; }
        public DateTime? PatchingEndDate { get; set; }
        public long CountOfDocsPatched { get; set; }
        public decimal PatchingRatePerMinute { get; set; }
        public string Note { get; set; }

        private PerformanceCounter cpuCounter;

        public long PhysicalRAM_Available { get; set; }
        public long PhysicalRAM_Total { get; set; }
        public decimal PhysicalRAM_FreePerc { get; set; }
        public decimal PhysicalRAM_OccupiedPerc { get; set; }

        public long CommittedRAM_Total { get; set; }
        public long CommittedRAM_Peak { get; set; }

        public int CurrentCPUPercentage => Convert.ToInt32(cpuCounter.NextValue());
        public int MaxCPUPercentage => Stats.Max(z => z.CPUPercentage);
        public decimal AvgCPUPercentage => (decimal)Stats.Average(z => z.CPUPercentage);


        public List<Stat> Stats { get; set; }

        #endregion


        public void StartTimer()
        {
            stopWatchTotalTime.Start();
        }
        

        public void Report()
        {
            this.LastUpdated = DateTime.Now;
            RefreshDatabaseStatus();
            CalculateRates();
            CaptureStats();

            Console.Clear();
            "*************************************************************************".WriteLine(Color.CornflowerBlue);
            var perfMonColor = Color.Pink;
            $"CPU                 : Current   : ".Write(Color.Gray); $"{CurrentCPUPercentage}%".Write(perfMonColor); " Avg:".Write(Color.Gray); $"{AvgCPUPercentage:##0.#}%".Write(perfMonColor); " Max:".Write(Color.Gray); $"{MaxCPUPercentage}%".WriteLine(perfMonColor);
            
            $"\nPhysical RAM        : Total     : ".Write(Color.Gray); $"{ PhysicalRAM_Total:##,###} MiB".WriteLine(perfMonColor);
            $"                    : Available : ".Write(Color.Gray); $"{ PhysicalRAM_Available:##,###} MiB".WriteLine(perfMonColor);
            $"                    : Free      : ".Write(Color.Gray); $"{ PhysicalRAM_FreePerc:##0.#}% ".WriteLine(perfMonColor);

            $"\nCommitted RAM       : Total     : ".Write(Color.Gray); $"{ CommittedRAM_Total:##,###} MiB".WriteLine(perfMonColor);
            $"                    : Peak      : ".Write(Color.Gray); $"{ CommittedRAM_Peak:##,###} MiB ".WriteLine(perfMonColor);

            var dbInfoColor = Color.CadetBlue;
            $"\nRavenURL            : ".Write(Color.Gray);this.RavenURL.WriteLine(dbInfoColor);
            $"LastDBRefresh       : ".Write(Color.Gray); $"{this.LastDatabaseRefresh:G}".WriteLine(dbInfoColor);
            $"Docs in DB          : ".Write(Color.Gray); $"{ this.CountOfDocumentsInDB:##,###}".WriteLine(dbInfoColor);
            if (indexes != null)
            {
                $"Indexes (Count:{this.CountOfIndexes})   :".Write(Color.Gray); " Stale: ".Write(Color.DeepPink); $"{this.staleIndexes?.Count}".WriteLine(Color.DeepPink);
                foreach (var index in indexes)
                {
                    var color = index.IsStale ? Color.DeepPink : Color.Gray;
                    $"   - IsStale:{index.IsStale} Name:{index.Name} ".WriteLine(color);
                }
            }

            var importColor = Color.GreenYellow;
            $"\nImport Start        : ".Write(Color.Gray); $"{this.ImportStartDate:G}".WriteLine(importColor);
            $"Import End          : ".Write(Color.Gray); $"{this.ImportEndDate:G}".WriteLine(importColor);
            $"Imported Docs       : ".Write(Color.Gray); $"{this.CountOfDocsImported:##,###}".WriteLine(importColor);
            $"Import Rate         : ".Write(Color.Gray); if(this.ImportRatePerMinute > 0) $"{this.ImportRatePerMinute:##,###} Docs/Min".Write(importColor); ; "".WriteLine();

            var indexRateColor = Color.Aquamarine;
            $"\nIndex Start         : ".Write(Color.Gray); $"{this.IndexesCreationStartDate:G}".WriteLine(indexRateColor);
            $"Index End           : ".Write(Color.Gray); $"{this.IndexesCreationEndDate:G}".WriteLine(indexRateColor);
            $"Mins To Create Idx  : ".Write(Color.Gray); if (this.IndexCreationMins > 0) $"{this.IndexCreationMins:##,##0} Mins".Write(indexRateColor); "".WriteLine();

            var patchColor = Color.Coral;
            $"\nPatching Start      : ".Write(Color.Gray);$"{this.PatchingStartDate:G}".WriteLine(patchColor);
            $"Patching End        : ".Write(Color.Gray); $"{this.PatchingEndDate:G}".WriteLine(patchColor);
            $"Patched Docs        : ".Write(Color.Gray); $"{this.CountOfDocsPatched:##,###}".WriteLine(patchColor);
            $"Patch Rate          : ".Write(Color.Gray); if(this.PatchingRatePerMinute > 0)$"{this.PatchingRatePerMinute:##,###} Docs/Min".Write(patchColor); "".WriteLine(); ;

            $"\nStatus              : ".Write(Color.Gray); this.Status.WriteLine(Color.Yellow);
            this.Status = "";
            $"Elapsed Time        : ".Write(Color.Gray); $"{stopWatchTotalTime.Elapsed.Hours:00}:{stopWatchTotalTime.Elapsed.Minutes:00}:{stopWatchTotalTime.Elapsed.Seconds:00}".WriteLine(Color.LawnGreen);
            "*************************************************************************".WriteLine(Color.CornflowerBlue);

            Thread.Sleep(50);
        }



        private void CalculateRates()
        {
            //patching
            if (PatchingEndDate.IsNotNull() && PatchingStartDate.IsNotNull() && CountOfDocsPatched > 0)
            {
                var mins = PatchingEndDate.Value.Subtract(PatchingStartDate.Value).TotalMinutes;
                this.PatchingRatePerMinute = Convert.ToDecimal(this.CountOfDocsPatched / mins);
            }
            else
            {
                this.PatchingRatePerMinute = 0;
            }

            //importing
            if (ImportEndDate.IsNotNull() && ImportStartDate.IsNotNull() && CountOfDocsImported > 0)
            {
                var mins = ImportEndDate.Value.Subtract(ImportStartDate.Value).TotalMinutes;
                this.ImportRatePerMinute = Convert.ToDecimal(this.CountOfDocsImported / mins);
             }
            else
            {
                this.ImportRatePerMinute = 0;
            }

            //index creation
            if (IndexesCreationEndDate.IsNotNull() && IndexesCreationStartDate.IsNotNull())
            {
                var mins = IndexesCreationEndDate.Value.Subtract(IndexesCreationStartDate.Value).TotalMinutes;
                this.IndexCreationMins = Convert.ToDecimal(mins);
            }

            //Memory
            PhysicalRAM_Available = PerformanceInfo.GetPhysicalAvailableMemoryInMiB();
            PhysicalRAM_Total = PerformanceInfo.GetTotalMemoryInMiB();
            PhysicalRAM_FreePerc = ((decimal)PhysicalRAM_Available / (decimal)PhysicalRAM_Total) * 100;
            PhysicalRAM_OccupiedPerc = 100 - PhysicalRAM_FreePerc;
            CommittedRAM_Total = PerformanceInfo.GetCommittedTotalMemoryInMiB();
            CommittedRAM_Peak = PerformanceInfo.GetCommittedPeakMemoryInMiB();

        }

        


        public void RefreshDatabaseStatus(bool forceUpdate = false)
        {
            //only update a max of every 5 seconds
            var delta = DateTime.Now.Subtract(LastDatabaseRefresh).TotalSeconds;
            //$"DELTA: {delta}".WriteLine(Color.PeachPuff);
            //$"FORCE: {forceUpdate}".WriteLine(Color.PeachPuff);
            if (forceUpdate || delta > 5)
            {
                //$"REFRESHING DB".WriteLine(Color.PeachPuff);
                var admin = Store.Maintenance.ForDatabase(this.DatabaseId);

                var databaseStatistics = admin.Send(new GetStatisticsOperation());
                this.indexes = databaseStatistics.Indexes.Where(x => x.State != IndexState.Disabled);
                this.staleIndexes = indexes.Where(z => z.IsStale).ToList();

                this.CountOfDocumentsInDB = databaseStatistics.CountOfDocuments;
                this.CountOfIndexes = databaseStatistics.CountOfIndexes;

                this.LastDatabaseRefresh = DateTime.Now;
            };
        }

        public void WaitForIndexing()
        {
            this.IsWaitingForIndexingToComplete = true;
            var startOfWaitingForIndexing = stopWatchTotalTime.Elapsed;
            
            while (this.IsWaitingForIndexingToComplete)
            {
                RefreshDatabaseStatus(forceUpdate:true);
                
                if (staleIndexes == null || staleIndexes.Count == 0)
                {
                    this.IsWaitingForIndexingToComplete = false;
                    RefreshDatabaseStatus(forceUpdate: true);
                }
                else
                {
                    ReportStatus($"Waited {stopWatchTotalTime.Elapsed.Subtract(startOfWaitingForIndexing):g} for non-stale indexes.");
                }
                
                Thread.Sleep(5000);
            }
        }

        public void ReportStatus(string status)
        {
            this.Status = status;
            Report();
        }

        private void InitialiseCPUCounter()
        {
            cpuCounter = new PerformanceCounter(
                "Processor",
                "% Processor Time",
                "_Total",
                true
            );
        }

 
        public void Dispose()
        {
            cpuCounter?.Dispose();
            Store?.Dispose();
        }

        private void CaptureStats()
        {

            //Console.WriteLine("Available Physical Memory (MiB) " + RAMPhysicalAvailable.ToString());
            //Console.WriteLine("Total Memory (MiB) " + RAMPhysicalTotal.ToString());
            //Console.WriteLine("Free (%) " + RAMPhysicalFreePercentage.ToString());
            //Console.WriteLine("Occupied (%) " + RAMPhysicalOccupiedPercentage.ToString());

            var s = new Stat
            {
                TimeStamp = DateTime.Now,
                CPUPercentage = CurrentCPUPercentage,
                DocsInDB = CountOfDocumentsInDB,
                ImportRatePerMinute = ImportRatePerMinute,
                PatchingRatePerMinute =  PatchingRatePerMinute,
                PatchedDocCount = CountOfDocsPatched,
                PhysicalRAM_Total = PhysicalRAM_Total,
                PhysicalRAM_Available = PhysicalRAM_Available,
                PhysicalRAM_FreePerc = PhysicalRAM_FreePerc,
                PhysicalRAM_OccupiedPerc = PhysicalRAM_OccupiedPerc,
                CommittedRAM_Total = CommittedRAM_Total,
                CommittedRAM_Peak = CommittedRAM_Peak

            };

            Stats.Add(s);
        }

    }

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
    }
}