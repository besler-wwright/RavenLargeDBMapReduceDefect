using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
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
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            Console.SetWindowSize(65, 40);

            "Loading appsettings.json".WriteLine(Color.Pink);
            var settings = GetSettings();
            settings.ToJSONPretty().WriteLine(Color.Aquamarine);

            if (settings.Mode == "Raven")
            {
                await ExecuteRavenMode(settings);
            }

            if (settings.Mode == "File")
            {
                var sw = new Stopwatch();
                sw.Start();
                WriteSmallFiles(settings, sw);
                WriteBigFile(settings, sw);
            }


            "Complete".WriteLine(Color.CornflowerBlue, true);

        }

        private static async Task ExecuteRavenMode(MySettings settings)
        {
            IDocumentStore store = new DocumentStore {Database = settings.DatabaseId, Certificate = new X509Certificate2(settings.CertPath), Urls = new[] {settings.RavenURL}};
            store.Initialize();

            var reporter = new Reporter(store) {Note = settings.Note};
            try
            {
                reporter.ReportStatus($"Starting {settings.Mode} Mode");
                if (settings.ImportDocs) await InsertDocuments(settings, reporter, store);
                if (settings.CreateIndexes) CreateIndexesWaitForNonStale(store, reporter, settings);
                if (settings.PatchDocs) PatchSomeRecords(store, reporter);
                if (settings.WaitForIndexesToNotBeStale) reporter.WaitForIndexing();
            }
            catch (Exception e)
            {
                reporter.ExceptionMessage = e.Message;
                reporter.StackTrace = e.StackTrace;

                Console.WriteLine(e);
                //throw;
            }
            finally
            {
                using (var rs = store.OpenSession())
                {
                    rs.Store(reporter);
                    rs.SaveChanges();
                }
            }
        }

        private static void WriteBigFile(MySettings settings, Stopwatch sw)
        {
            var BigFileStart = sw.Elapsed;
            var file = Path.Join(settings.TempDir, $"big-file.txt");
            decimal gbToWrite = settings.LargeFileSizeInGB; //0.000001 = KB, 0.001 = MB, 1.0 = GB
            WriteDummyFile(gbToWrite, file);
            Console.WriteLine($"Time to write big file: {sw.Elapsed.Subtract(BigFileStart):g} [{Convert.ToInt64(gbToWrite * 1024 * 1024 * 1024):##,##0} bytes]");
        }



        private static void WriteSmallFiles(MySettings settings, Stopwatch sw)
        {
            var fileWriteStart = sw.Elapsed;
            int CountOfSmallFiles = settings.CountOfSmallFilesToWrite;
            decimal gbToWrite = settings.SmallFileSizeInGB; //0.000001 = KB, 0.001 = MB, 1.0 = GB
            for (var i = 0; i < CountOfSmallFiles; i++)
            {
                var file = Path.Join(settings.TempDir, $"small-file-{i:000}.txt");
                WriteDummyFile(gbToWrite, file);
            }

            Console.WriteLine($"Time to write {CountOfSmallFiles} files: {sw.Elapsed.Subtract(fileWriteStart):g} [{Convert.ToInt64(gbToWrite * 1024 * 1024 * 1024 * CountOfSmallFiles):##,##0} bytes]");
        }

        private static void WriteDummyFile(decimal sizeInGB, string file)
        {
            const char text = '0';
            var totalSize = Convert.ToInt64(sizeInGB * 1024 * 1024 * 1024);

            using (StreamWriter outfile = new StreamWriter(file))
            {
                for (long i = 0; i < totalSize; i++)
                {
                    outfile.Write(text);
                }

            }
        }

        private static async Task InsertDocuments(MySettings settings, Reporter reporter, IDocumentStore store)
        {
            //this method optionally creates multiple threads 

            var maxImportThreads = settings.MaxInsertThreads;

            reporter.ImportStartDate = DateTime.Now;

            while (reporter.CurrentCountOfDocumentsInDB < settings.MinimumDocumentCount)
            {
                reporter.RefreshDatabaseStatus(true);
                var threadsNeededToGetAllDocs = Convert.ToInt32(settings.MinimumDocumentCount / settings.CountOfDocsToInsertPerThread);
                var threadsToUse = Math.Min(threadsNeededToGetAllDocs, maxImportThreads);
                var docsToGo = settings.MinimumDocumentCount - reporter.CurrentCountOfDocumentsInDB;
                var maxDocsPerThread = Convert.ToInt32(docsToGo / threadsToUse);
                var docsPerThread = Math.Min(maxDocsPerThread, settings.CountOfDocsToInsertPerThread);

                reporter.ReportStatus($"Importing {docsPerThread} docs/thread with {threadsToUse} threads ...");

                var tasks = new List<Task<int>>();
                for (int i = 0; i < threadsToUse; i++)
                {
                    tasks.Add(InsertRandomDocsAsync(store, reporter.DatabaseId, docsPerThread));
                }

                await Task.WhenAny(tasks);

                reporter.ImportLastUpdateDate = DateTime.Now;
                
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

        private static void CreateIndexesWaitForNonStale(IDocumentStore store, Reporter reporter, MySettings settings)
        {
            reporter.IndexesCreationStartDate = DateTime.Now;
            
            //add the indexes
            new ChargeDataSearchIndex().Execute(store);
            new PaymentDataSearchIndex().Execute(store);
            new PatientTotalByGLDeptByGLAccountIndex().Execute(store);

            //wait for indexing to see if error occurs
            if (settings.WaitForIndexesToNotBeStale)
            {
                reporter.WaitForIndexing();
            }
            

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
                    .WhereLessThan(z => z.Amount, -5000)
                    .AndAlso()
                    .Not
                    .ContainsAny(z => z.TagIds, new List<string>{ tag.Id } );
                    

                var patchByQueryOperation = BuildAddTagPatchByQueryOperation(query, denormalizedTag, user, true);


                // Do not add await here - allow method to continue
                PerformTagPatch(store, patchByQueryOperation, reporter);

            }

            reporter.PatchingLastUpdateDate = DateTime.Now;
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
                    reporter.PatchingLastUpdateDate = DateTime.Now; //by doing this here,we get to see the rate as we go
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
                $@"//declare function removeDuplicates(myArr, prop) 
                   //{{
                   //  return myArr.filter((obj, pos, arr) => 
                   //  {{
                   //     return arr.map(mapObj => mapObj[prop]).indexOf(obj[prop]) === pos;
                   //  }});
                   //}}
                   declare function addTag(currentTags, newTag) {{
                     let newTags = currentTags || [];
                     newTags.push(newTag);
                     return newTags; //removeDuplicates(newTags, 'Id');
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