using Newtonsoft.Json.Linq;
using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace ElasticSearchQueryBuilder
{
    public class IndexQueryMap : IReadOnlyDictionary<string, string>
    {
        readonly IndexFiltersMap _filtersMap;
        readonly bool _evaluateFiltersAsOr;

        public IEnumerable<string> Keys => GetIndexQueryMap().Keys;
        public IEnumerable<string> Values => GetIndexQueryMap().Values;
        public int Count => GetIndexQueryMap().Count;
        public string this[string key] => GetIndexQueryMap()[key];

        public IndexQueryMap(IndexFiltersMap filtersMap, bool evaluateFiltersAsOr = false)
        {
            _filtersMap = filtersMap ?? throw new ArgumentNullException(nameof(filtersMap));
            _evaluateFiltersAsOr = evaluateFiltersAsOr;
        }

        IReadOnlyDictionary<string, string> GetIndexQueryMap()
        {
            var output = new Dictionary<string, string>();

            foreach (var indexFilters in _filtersMap)
            {
                var mandatoryQueries = indexFilters.Value.Where(filter => filter.IsMandatory).Select(filter => filter.Query);
                var notMandatoryQueries = indexFilters.Value.Where(filter => !filter.IsMandatory).Select(filter => filter.Query);

                var query = new JObject
                {
                    ["bool"] = new JObject
                    {
                        ["must"] = new JArray(),
                        ["should"] = new JArray(),
                        ["must_not"] = new JArray(),
                    }
                };

                var boolQuery = query.Value<JObject>("bool");
                if (mandatoryQueries.Any())
                {
                    if (_evaluateFiltersAsOr)
                    {
                        var shouldQueries = boolQuery?.Value<JArray>("should");
                        foreach (var mandatoryquery in mandatoryQueries)
                            shouldQueries?.Add(mandatoryquery);
                    }
                    else
                    {
                        var mustQueries = boolQuery?.Value<JArray>("must");
                        foreach (var mandatoryquery in mandatoryQueries)
                            mustQueries?.Add(mandatoryquery);
                    }
                }

                if (notMandatoryQueries.Any())
                {
                    var mustNotQueries = boolQuery?.Value<JArray>("must_not");
                    if (_evaluateFiltersAsOr)
                    {
                        mustNotQueries?.Add(new JObject
                        {
                            ["bool"] = new JObject
                            {
                                ["should"] = JArray.FromObject(notMandatoryQueries)
                            }
                        });
                    }
                    else
                    {
                        foreach (var notMandatoryQuery in notMandatoryQueries)
                            mustNotQueries?.Add(notMandatoryQuery);
                    }
                }

                output[indexFilters.Key] = query.ToString();
            }

            return output;
        }

        public bool ContainsKey(string key) => GetIndexQueryMap().ContainsKey(key);

        public bool TryGetValue(string key, [MaybeNullWhen(false)] out string value) => GetIndexQueryMap().TryGetValue(key, out value);

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => GetIndexQueryMap().GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
