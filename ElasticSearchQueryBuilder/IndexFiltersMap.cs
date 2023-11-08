using ElasticSearchQueryBuilder.Enums;
using ElasticSearchQueryBuilder.Models;
using ElasticSearchQueryBuilder.Models.Abstracts;
using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace ElasticSearchQueryBuilder
{
    public class IndexFiltersMap : IReadOnlyDictionary<string, IEnumerable<Filter>>
    {
        readonly IEnumerable<FilterSpecification> _specifications;

        public IEnumerable<string> Keys => GetIndexFiltersMap().Keys;
        public IEnumerable<IEnumerable<Filter>> Values => GetIndexFiltersMap().Values;
        public int Count => GetIndexFiltersMap().Count;
        public IEnumerable<Filter> this[string key] => GetIndexFiltersMap()[key];

        public IndexFiltersMap(IEnumerable<FilterSpecification> specifications)
        {
            if (specifications is null)
                throw new ArgumentNullException(nameof(specifications));

            if (specifications.Any() == false)
                throw new ArgumentException($"{nameof(specifications)} can't be empty");

            _specifications = specifications;
        }

        IReadOnlyDictionary<string, IEnumerable<Filter>> GetIndexFiltersMap()
        {
            var output = new Dictionary<string, IEnumerable<Filter>>();

            // Reduce the filters to use
            var specifications = _specifications.GroupBy(spec => new
            {
                spec.IndexName,
                spec.FieldName,
                spec.Operator,
                spec.Type,
                spec.NestedOf,
            }).Select(filterGroup => new FilterSpecification
            {
                IndexName = filterGroup.Key.IndexName,
                FieldName = filterGroup.Key.FieldName,
                NestedOf = filterGroup.Key.NestedOf,
                Operator = filterGroup.Key.Operator,
                Type = filterGroup.Key.Type,
                Values = filterGroup.SelectMany(filter => filter.Values ?? Enumerable.Empty<string>())
            });

            // Convert the filters to elastic filters
            foreach (var specificationsGroup in specifications.GroupBy(spec => spec.IndexName))
            {
                var elasticFilters = specificationsGroup.Select(spec =>
                {
                    Filter elasticFilter = spec.Type switch
                    {
                        FilterType.Text => new TextFilter(spec),
                        FilterType.Number or FilterType.Date => new DataFilter(spec),
                        _ => throw new NotSupportedException($"The specification type '{spec.Type}' is not recognized"),
                    };

                    return elasticFilter;
                });

                output[specificationsGroup.Key] = elasticFilters;
            }

            return output;
        }

        public bool ContainsKey(string key) => GetIndexFiltersMap().ContainsKey(key);

        public bool TryGetValue(string key, [MaybeNullWhen(false)] out IEnumerable<Filter> value) => GetIndexFiltersMap().TryGetValue(key, out value);

        public IEnumerator<KeyValuePair<string, IEnumerable<Filter>>> GetEnumerator() => GetIndexFiltersMap().GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetIndexFiltersMap().GetEnumerator();
    }
}
