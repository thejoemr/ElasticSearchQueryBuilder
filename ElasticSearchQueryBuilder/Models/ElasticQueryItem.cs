namespace ElasticSearchQueryBuilder.Models
{
    public class ElasticQueryItem
    {
        public string JsonQuery { get; }
        public bool IsMandatory { get; }

        public ElasticQueryItem(string jsonQuery, bool isMandatory)
        {
            JsonQuery = jsonQuery;
            IsMandatory = isMandatory;
        }
    }
}
