using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using Raven.Client.Documents;
using Raven.Client.Documents.Indexes;
using Raven.Client.Documents.Operations;
using Raven.Client.Documents.Queries;
using Raven.Client.Documents.Session;

namespace RavenDBTestApril2019
{
    class Program
    {
        static void Main(string[] args)
        {
            "Initializing...".WriteLine(Color.Pink);

            var databaseID = "BIG_DATA";
            var certPath = @"C:\Raven\Server\cluster.server.certificate.beslerwade.pfx";
            var ravenURL = "https://a.beslerwade.development.run";

            IDocumentStore store = new DocumentStore {Database = databaseID, Certificate = new X509Certificate2(certPath), Urls = new[] {ravenURL}};
            store.Initialize();

            var reporter = new Reporter(store);
            ImportRecords(store, reporter);
            CreateIndexesWaitForNonStale(store, reporter);
            PatchSomeRecords(store, reporter);
            reporter.WaitForIndexing();

            "Complete".WriteLine(Color.CornflowerBlue, true);

        }

        private static void PatchSomeRecords(IDocumentStore store, Reporter reporter)
        {

            reporter.ReportStatus("Starting Patching");

            //patch some records
            var tag = new Tag
            {
                Id = $"Tag-MySecretTagId",
                Name = "SomeTag",
                Criteria = "Some Criteria - Does not matter"
            };

            DenormalizedTagReference<Tag> denormalizedTag = tag;

            var user = new User
            {
                Id = $"User-Wade",
                Name = "Wade Wright",
                UserName = "wwright@besler.com"
            };

            using (var rs = store.OpenSession())
            {
                var query = rs.Advanced
                    .DocumentQuery<PaymentDataSearchIndex.Result, PaymentDataSearchIndex>()
                    .WhereLessThan(z => z.Amount, -500);

                var patchByQueryOperation = BuildAddTagPatchByQueryOperation(query, denormalizedTag, user, true);


                // Do not add await here - allow method to continue
                PerformTagPatch(store, patchByQueryOperation, reporter);

            }

            reporter.ReportStatus("Patching Command(s) sent");
        }

        public static void PerformTagPatch(IDocumentStore store, IOperation<OperationIdResult> batchOperation, Reporter reporter)
        {
            try
            {
                var operation = store.Operations.ForDatabase(reporter.DatabaseId).Send(batchOperation);

                operation.OnProgressChanged = x =>
                {
                    DeterminateProgress progress = (DeterminateProgress)x;
                    var perc = (((decimal)progress.Processed / (decimal)progress.Total) * 100);
                    reporter.ReportStatus($"Tag Progress: { perc :##,##0.0###}%");
                };

                var result = operation.WaitForCompletion<BulkOperationResult>();
                reporter.TaggingCompletionDate = DateTime.Now;
                var formattedResults =
                    result.Details
                        .Select(x => (BulkOperationResult.PatchDetails)x)
                        .GroupBy(x => x.Status)
                        .Select(x => $"{x.Key}: {x.Count()}").ToList();
                reporter.ReportStatus(formattedResults.ToJSONPretty());

            }
            catch (Exception ex)
            {
                ex.Message.WriteLine(Color.Red);
            }
            finally
            {
                "Successfully executed path".WriteLine(Color.Cyan);
            }
        }


        private static void ImportRecords(IDocumentStore store, Reporter reporter)
        {
            var countOfEachImported = 0;
            const int Million = 1000000;
            const int RecordsPerLoop = 10000;

            var loopCount = 0;
            while (countOfEachImported < (.01 * Million))
            {
                loopCount++;
                using (var bulkInsert = store.BulkInsert(reporter.DatabaseId))
                {
                    while (countOfEachImported < RecordsPerLoop * loopCount)
                    {
                        var charge = Charge.GenerateRandomCharge();
                        bulkInsert.Store(Payment.GenerateNewFromCharge(charge));
                        bulkInsert.Store(charge);
                        countOfEachImported += 1;
                    }
                }
                
                //report each loop
                reporter.ReportStatus($"Imported {countOfEachImported:##,##0} payments and {countOfEachImported:#0,###} charges");
            }

            reporter.ImportCompletedDate = DateTime.Now;
        }

        private static void CreateIndexesWaitForNonStale(IDocumentStore store, Reporter reporter)
        {
            //add the indexes
            new ChargeDataSearchIndex().Execute(store);
            new PaymentDataSearchIndex().Execute(store);
            //new PatientTotalByGLDeptByGLAccountIndex().Execute(store);

            reporter.IndexesCreationCCompleteDate = DateTime.Now;

            //wait for indexing to see if error occurs
            reporter.WaitForIndexing();
        }


        public static PatchByQueryOperation BuildAddTagPatchByQueryOperation<T>(IDocumentQuery<T> query, DenormalizedTagReference<Tag> denormalizedTag, User user, bool allowStale = false, bool retrieveDetails = false)
        {
            var queryParameters = query.GetIndexQuery().QueryParameters;
            var indexQuery = query.GetIndexQuery().Query;

            var script =
                $@"declare function removeDuplicates(myArr, prop) 
                   {{
                     return myArr.filter((obj, pos, arr) => 
                     {{
                        return arr.map(mapObj => mapObj[prop]).indexOf(obj[prop]) === pos;
                     }});
                   }}
                   declare function addTag(currentTags, newTag) {{
                     let newTags = currentTags || [];
                     newTags.push(newTag);
                     return removeDuplicates(newTags, 'Id');
                   }} 
                   {indexQuery}
                   update 
                   {{ 
                     this.Tags = addTag(this.Tags, {denormalizedTag.ToJSON()});
                     this.UpdatedBy = this.UpdatedBy || {{}};
                     this.UpdatedBy.Id = '{user.Id}';
                     this.UpdatedBy.Name = '{user.Name}';
                     this.UpdatedBy.UserName = '{user.UserName}';
                     this.UpdatedOnUTC = '{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss.fffffffZ}';
                   }}";

            var patchByQueryOperation = new PatchByQueryOperation(
                new IndexQuery
                {
                    QueryParameters = queryParameters,
                    Query = script
                },
                new QueryOperationOptions
                {
                    RetrieveDetails = retrieveDetails,
                    AllowStale = allowStale
                });

            return patchByQueryOperation;
        }

    }

    public class Reporter
    {
        public Reporter(IDocumentStore store)
        {
            this.Store = store;
            this.DatabaseId = store.Database;
            this.RavenURL = store.Urls.First();
            StartTimer();
            this.WaitForIndexing();
        }
        private readonly Stopwatch stopWatchTotalTime = new Stopwatch();
        public bool IsWaitingForIndexingToComplete { get; set; }
        public IDocumentStore Store { get; set; }
        public string DatabaseId { get; set; }
        public string RavenURL { get; set; }
        
        private TimeSpan startOfPatching { get; set; }
        private IEnumerable<IndexInformation> indexes { get; set; }
        private List<IndexInformation> staleIndexes { get; set; }
        private string Status { get; set; }
        public long CountOfDocuments { get; set; }
        public int CountOfIndexes { get; set; }
        public DateTime LastDatabaseRefresh { get; set; }
        public DateTime? ImportCompletedDate { get; set; }
        public DateTime? IndexesCreationCCompleteDate { get; set; }
        public DateTime? TaggingCompletionDate { get; set; }

        public void StartTimer()
        {
            stopWatchTotalTime.Start();
        }


        public void Report()
        {
            RefreshDatabaseStatus();

            Console.Clear();
            "*****************************************************".WriteLine(Color.CornflowerBlue);
            $"RavenURL     : ".Write(Color.Gray); this.RavenURL.WriteLine(Color.CadetBlue);
            $"LastDBRefresh: ".Write(Color.Gray); $"{this.LastDatabaseRefresh:G}".WriteLine(Color.GreenYellow);
            $"Import Done  : ".Write(Color.Gray); $"{this.ImportCompletedDate:G}".WriteLine(Color.GreenYellow);
            $"Index Create : ".Write(Color.Gray); $"{this.IndexesCreationCCompleteDate:G}".WriteLine(Color.GreenYellow);
            $"Tagging Done : ".Write(Color.Gray); $"{this.TaggingCompletionDate:G}".WriteLine(Color.GreenYellow);
            $"Doc Count    : ".Write(Color.Gray); $"{ this.CountOfDocuments:##,###}".WriteLine(Color.WhiteSmoke);

            if (indexes != null)
            {
                $"Indexes      : Count:".Write(Color.Gray); $"{this.CountOfIndexes}".Write(Color.Khaki); " Stale: ".Write(Color.DarkRed); $"{this.staleIndexes?.Count}".WriteLine(Color.DeepPink);
                foreach (var index in indexes)
                {
                    var color = index.IsStale ? Color.DeepPink : Color.Green;
                    $"   - IsStale:{index.IsStale} Name:{index.Name} ".WriteLine(color);
                }
            }
            $"Status       : ".Write(Color.Gray); this.Status.WriteLine(Color.Pink);
            this.Status = "";
            $"Elapsed Time : ".Write(Color.Gray); $"{stopWatchTotalTime.Elapsed.Hours:00}:{stopWatchTotalTime.Elapsed.Minutes:00}:{stopWatchTotalTime.Elapsed.Seconds:00}".WriteLine(Color.LawnGreen);
            "*****************************************************".WriteLine(Color.CornflowerBlue);
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

                this.CountOfDocuments = databaseStatistics.CountOfDocuments;
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
    }
        
    

}

