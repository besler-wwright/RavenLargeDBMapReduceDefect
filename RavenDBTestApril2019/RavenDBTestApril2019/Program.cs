using System;
using System.Collections.Generic;
using System.Configuration;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Raven.Client.Documents;
using Raven.Client.Documents.Operations;
using Raven.Client.Documents.Queries;
using Raven.Client.Documents.Session;


namespace RavenDBTestApril2019
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.SetWindowSize(75,40);
            

            "Loading appsettings.json".WriteLine(Color.Pink);
            var settings = GetSettings();
            settings.ToJSONPretty().WriteLine(Color.Aquamarine);

            IDocumentStore store = new DocumentStore {Database = settings.DatabaseId, Certificate = new X509Certificate2(settings.CertPath), Urls = new[] {settings.RavenURL}};
            store.Initialize();

            var reporter = new Reporter(store) {Note = settings.Note};

            reporter.ReportStatus("Starting...");

            await InsertDocuments(settings, reporter, store);
            if(settings.CreateIndexes) CreateIndexesWaitForNonStale(store, reporter);
            if(settings.PatchDocs)PatchSomeRecords(store, reporter);
            reporter.WaitForIndexing();

            using (var rs = store.OpenSession())
            {
                rs.Store(reporter);
                rs.SaveChanges();
            }


            "Complete".WriteLine(Color.CornflowerBlue, true);

        }

        private static async Task InsertDocuments(MySettings settings, Reporter reporter, IDocumentStore store)
        {
            //this method creates multiple threads to insert documents faster

            const int Million = 1000000;
            var importThreads = settings.CountOfInsertThreads;

            if (settings.ImportDocs)
            {
                var docsAdded = 0;
                reporter.ImportStartDate = DateTime.Now;

                while (reporter.CountOfDocsImported < (settings.MillionsOfDocsToImport * Million))
                {
                    var tasks = new List<Task<int>>();
                    for (int i = 0; i < importThreads; i++)
                    {
                        tasks.Add(InsertRandomDocsAsync(store, reporter.DatabaseId, settings.CountOfDocsToInsertPerThread));
                    }

                    await Task.WhenAll(tasks);

                    docsAdded += tasks.Sum(z => z.Result);
                    for (int i = 0; i < importThreads; i++)
                    {
                        reporter.CountOfDocsImported += tasks[i].Result;
                    }

                    reporter.ReportStatus($"Imported {docsAdded:##,##0} docs");
                    reporter.ImportEndDate = DateTime.Now;
                }
            }
        }


        private static MySettings GetSettings()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

            IConfigurationRoot configuration = builder.Build();

            var settings = new MySettings();
            configuration.Bind(settings);
            return settings;
        }


        private static async Task<int> InsertRandomDocsAsync(IDocumentStore store, string dbId, int countOfDocsToInsert)
        {
            var docsInserted = 0;
            using (var bulkInsert = store.BulkInsert(dbId))
            {
                while (docsInserted < countOfDocsToInsert)
                {
                    var charge = Charge.GenerateRandomCharge();
                    bulkInsert.Store(Payment.GenerateNewFromCharge(charge));
                    bulkInsert.Store(charge);
                    docsInserted += 2;
                }
            }
            return docsInserted;
        }

        private static void CreateIndexesWaitForNonStale(IDocumentStore store, Reporter reporter)
        {
            reporter.IndexesCreationStartDate = DateTime.Now;
            
            //add the indexes
            new ChargeDataSearchIndex().Execute(store);
            new PaymentDataSearchIndex().Execute(store);
            new PatientTotalByGLDeptByGLAccountIndex().Execute(store);

            //wait for indexing to see if error occurs
            reporter.WaitForIndexing();

            reporter.IndexesCreationEndDate = DateTime.Now;
        }

        private static void PatchSomeRecords(IDocumentStore store, Reporter reporter)
        {
            reporter.PatchingStartDate = DateTime.Now;
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
                    .WhereLessThan(z => z.Amount, -5000);

                var patchByQueryOperation = BuildAddTagPatchByQueryOperation(query, denormalizedTag, user, true);


                // Do not add await here - allow method to continue
                PerformTagPatch(store, patchByQueryOperation, reporter);

            }

            reporter.PatchingEndDate = DateTime.Now;
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
                    reporter.CountOfDocsPatched = progress.Processed;
                    reporter.PatchingEndDate = DateTime.Now; //by doing this here,we get to see the rate as we go
                    reporter.ReportStatus($"Tag Progress: { perc:##,##0.0###}%   [{progress.Processed:##,##0} Tagged of {progress.Total:##,##0}]");
                };

                var result = operation.WaitForCompletion<BulkOperationResult>();
                
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
}

public class MySettings
{
    public string Note { get; set; }
    public string DatabaseId { get; set; }
    public string CertPath { get; set; }
    public string RavenURL { get; set; }
    public decimal MillionsOfDocsToImport { get; set; }
    public bool ImportDocs { get; set; }
    public bool CreateIndexes { get; set; }
    public bool PatchDocs { get; set; }
    public int CountOfInsertThreads { get; set; }
    public int CountOfDocsToInsertPerThread { get; set; }
}