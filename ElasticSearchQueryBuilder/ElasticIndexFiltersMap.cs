using ElasticSearchQueryBuilder.Enums;
using ElasticSearchQueryBuilder.Models;
using ElasticSearchQueryBuilder.Models.Abstracts;
using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace ElasticSearchQueryBuilder
{
    public class ElasticIndexFiltersMap : IReadOnlyDictionary<string, IEnumerable<ElasticFilter>>
    {
        public IEnumerable<Filter> Source { get; }
        public bool EvaluateAsOr { get; }
        public IEnumerable<string> Keys => GetIndexFiltersMap().Keys;
        public IEnumerable<IEnumerable<ElasticFilter>> Values => GetIndexFiltersMap().Values;
        public int Count => GetIndexFiltersMap().Count;
        public IEnumerable<ElasticFilter> this[string key] => GetIndexFiltersMap()[key];

        public ElasticIndexFiltersMap(IEnumerable<Filter> source, bool evaluateAsOr = false)
        {
            if (source is null)
                throw new ArgumentNullException(nameof(source));

            if (source.Any() == false)
                throw new ArgumentException($"{nameof(source)} can't be empty");

            Source = source;
            EvaluateAsOr = evaluateAsOr;
        }

        IReadOnlyDictionary<string, IEnumerable<ElasticFilter>> GetIndexFiltersMap()
        {
            var output = new Dictionary<string, IEnumerable<ElasticFilter>>();

            // Reduce the filters to use
            var filters = Source.GroupBy(spec => new
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
                Values = filterGroup.SelectMany(filter => filter.Values ?? Enumerable.Empty<string>())
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
                        _ => throw new NotSupportedException($"The filter type '{filter.Type}' is not recognized"),
                    };

                    return elasticFilter;
                });

                output[filterGroup.Key] = elasticFilters;
            }

            return output;
        }

        public bool ContainsKey(string key) => GetIndexFiltersMap().ContainsKey(key);

        public bool TryGetValue(string key, [MaybeNullWhen(false)] out IEnumerable<ElasticFilter> value) => GetIndexFiltersMap().TryGetValue(key, out value);

        public IEnumerator<KeyValuePair<string, IEnumerable<ElasticFilter>>> GetEnumerator() => GetIndexFiltersMap().GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetIndexFiltersMap().GetEnumerator();
    }
}
