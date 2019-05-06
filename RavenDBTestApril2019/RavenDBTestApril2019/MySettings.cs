namespace RavenDBTestApril2019
{
    public class MySettings
    {
        public string Mode { get; set; }
        public string TempDir { get; set; }
        public decimal LargeFileSizeInGB { get; set; }
        public decimal SmallFileSizeInGB { get; set; }
        public int CountOfSmallFilesToWrite { get; set; }
        public string Note { get; set; }
        public string DatabaseId { get; set; }
        public string CertPath { get; set; }
        public string RavenURL { get; set; }
        //public decimal MillionsOfDocsToImport { get; set; }
        public bool ImportDocs { get; set; }
        public int MinimumDocumentCount { get; set; }
        public bool CreateIndexes { get; set; }
        public bool PatchDocs { get; set; }
        public int MaxInsertThreads { get; set; }
        public int CountOfDocsToInsertPerThread { get; set; }
        public bool WaitForIndexesToNotBeStale { get; set; }
    }
}