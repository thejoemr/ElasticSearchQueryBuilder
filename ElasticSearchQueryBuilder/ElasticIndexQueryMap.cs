using ElasticSearchQueryBuilder.Enums;
using ElasticSearchQueryBuilder.Models;
using ElasticSearchQueryBuilder.Models.Abstracts;
using Newtonsoft.Json.Linq;

namespace ElasticSearchQueryBuilder
{
    public class ElasticIndexQueryMap
    {
        public IReadOnlyCollection<Filter> Filters { get; init; }
        public bool EvaluateFiltersAsOr { get; init; }
        public IReadOnlyDictionary<string, JObject> IndexQueryMap { get; init; }

        public ElasticIndexQueryMap(IReadOnlyCollection<Filter> filters, bool evaluateFiltersAsOr = false)
        {
            if (filters is null)
                throw new ArgumentNullException(nameof(filters));

            if (filters.Any() == false)
                throw new ArgumentException($"{nameof(filters)} can't be empty");

            Filters = filters;
            EvaluateFiltersAsOr = evaluateFiltersAsOr;
            IndexQueryMap = GetIndexQueryMap();
        }

        private IReadOnlyDictionary<string, IEnumerable<ElasticFilter>> GetIndexFiltersMap()
        {
            var output = new Dictionary<string, IEnumerable<ElasticFilter>>();

            // Reduce the filters to use
            var filters = Filters.GroupBy(spec => new
            {
                spec.IndexName,
                spec.FieldName,
                spec.Operator,
                spec.Type,
                spec.NestedOf,
            }).Select(filterGroup => new Filter
            {
                IndexName = filterGroup.Key.IndexName,
                FieldName = filterGroup.Key.FieldName,
                NestedOf = filterGroup.Key.NestedOf,
                Operator = filterGroup.Key.Operator,
                Type = filterGroup.Key.Type,
                Values = filterGroup.SelectMany(filter => filter.Values)
            });

            // Convert the filters to elastic filters
            foreach (var filterGroup in filters.GroupBy(filter => filter.IndexName))
            {
                var elasticFilters = filterGroup.Select(filter =>
                {
                    ElasticFilter elasticFilter = filter.Type switch
                    {
                        FilterType.Text => new ElasticTextFilter(filter),
                        FilterType.Number or FilterType.Date => new ElasticDataFilter(filter),
                        _ => throw new NotSupportedException(),
                    };

                    return elasticFilter;
                });

                output[filterGroup.Key] = elasticFilters;
            }

            return output;
        }

        private IReadOnlyDictionary<string, JObject> GetIndexQueryMap()
        {
            var output = new Dictionary<string, JObject>();

            foreach (var elasticFiltersMap in GetIndexFiltersMap())
            {
                var mustQueries = elasticFiltersMap.Value.Where(filter => filter.IsMustAssert)
                                                         .Select(filter => filter.Query);
                var mustNotQueries = elasticFiltersMap.Value.Where(filter => !filter.IsMustAssert)
                                                            .Select(filter => filter.Query);

                var query = new JObject()
                {
                    "bool", new JObject()
                };

                if (EvaluateFiltersAsOr)
                {
                    if (mustQueries.Any())
                        query.Value<JObject>("bool")?.Add("should", JArray.FromObject(mustQueries));

                    if (mustNotQueries.Any())
                        query.Value<JObject>("bool")?.Add("must_not", JArray.FromObject(new[]
                        {
                            new JObject
                            {
                                "bool", new JObject
                                {
                                    "should", JArray.FromObject(mustNotQueries)
                                }
                            }
                        }));
                }
                else
                {
                    if (mustQueries.Any())
                        query.Value<JObject>("bool")?.Add("must", JArray.FromObject(mustQueries));

                    if (mustNotQueries.Any())
                        query.Value<JObject>("bool")?.Add("must_not", JArray.FromObject(mustNotQueries));
                }

                output[elasticFiltersMap.Key] = query;
            }

            return output;
        }
    }
}
