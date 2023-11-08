namespace ElasticSearchQueryBuilder.Models
{
    internal class QueryItem
    {
        public string JsonQuery { get; }
        public bool IsMandatory { get; }

        public QueryItem(string jsonQuery, bool isMandatory)
        {
            if (string.IsNullOrEmpty(jsonQuery))
                throw new ArgumentException($"'{nameof(jsonQuery)}' can't be null or empty.", nameof(jsonQuery));

            JsonQuery = jsonQuery;
            IsMandatory = isMandatory;
        }
    }
}
