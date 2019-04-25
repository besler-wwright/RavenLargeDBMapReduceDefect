namespace RavenDBTestApril2019
{
    public class DenormalizedTagReference<T> where T : ITagDocument
    {
        public string Id { get; set; }
        public string Name { get; set; }

        public static implicit operator DenormalizedTagReference<T>(T doc)
        {
            return new DenormalizedTagReference<T>
            {
                Id = doc.Id,
                Name = doc.Name,
            };
        }

        public override string ToString()
        {
            return $"Id: {Id} Name: {Name}";
        }

    }
}