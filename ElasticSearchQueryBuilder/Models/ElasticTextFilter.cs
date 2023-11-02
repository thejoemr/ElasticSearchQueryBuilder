using ElasticSearchQueryBuilder.Enums;
using ElasticSearchQueryBuilder.Models.Abstracts;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;

namespace ElasticSearchQueryBuilder.Models
{
    internal class ElasticTextFilter : ElasticFilter
    {
        private bool IsMatch => new FilterOperator[]
        {
            FilterOperator.Is,
            FilterOperator.IsNot
        }.Contains(Specification.Operator);

        private bool IsWildcard => new FilterOperator[]
        {
            FilterOperator.Contains,
            FilterOperator.NotContains
        }.Contains(Specification.Operator);

        public override JObject Query { get; init; }

        public ElasticTextFilter(Filter specification) : base(specification)
        {
            if (!new FilterType[]
            {
                FilterType.Text
            }.Contains(specification.Type))
                throw new ArgumentException($"This is not a correct object for a '{specification.Type}' filter");

            if (new FilterOperator[]
            {
                FilterOperator.Between,
                FilterOperator.NotBetween,
                FilterOperator.GreaterThan,
                FilterOperator.LessThan,
            }.Contains(specification.Operator))
                throw new ArgumentException($"The operator '{specification.Operator}' is not valid for String filters");

            Query = GetQuery();
        }

        private IEnumerable<JObject> GetMatchQuery() => Specification.Values.Select(value => new JObject
        {
            "match", new JObject
            {
                Specification.FieldName, value
            }
        });

        private IEnumerable<JObject> GetWildcardQuery() => Specification.Values.Select(value =>
        {
            var tokens = Regex.Split(value ?? string.Empty, "[\\s]") // Split by white space to get all words
                            .Where(word => !string.IsNullOrEmpty(word) && Regex.IsMatch(word, "[\\s]")) // Get all not null, empty or words with only spaces
                            .SelectMany(word => Regex.Split(word, "[^\\w]")) // Get all the alphanumeric sub words (tokens)
                            .Where(token => !string.IsNullOrEmpty(token) && Regex.IsMatch(token, "[\\s]")) // Get all not null, empty or words with only spaces
                            .Select(token => $"*{token.ToLowerInvariant()}*");

            return new JObject
            {
                "bool", new JObject
                {
                    "must", JArray.FromObject(tokens.Select(token => new JObject
                    {
                        "wildcard", new JObject
                        {
                            Specification.FieldName, token
                        }
                    }))
                }
            };
        });

        private JObject GetQuery()
        {
            IEnumerable<JObject> queries;

            if (IsMatch)
                queries = GetMatchQuery();
            else if (IsWildcard)
                queries = GetWildcardQuery();
            else
                throw new NotImplementedException($"Specification filter is not cofigured correctly");

            return new JObject
            {
                "bool", new JObject
                {
                    Specification.EvaluateValuesAsOr ? "should" : "must", JArray.FromObject(queries)
                }
            };
        }
    }
}
