namespace RavenDBTestApril2019
{
    public class Tag : ITagDocument
    {
        public string Id { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        public string Criteria { get; set; }

        public string Color { get; set; }
    }
}