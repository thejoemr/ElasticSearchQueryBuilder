using ElasticSearchQueryBuilder.Models;
using Newtonsoft.Json.Linq;

namespace ElasticSearchQueryBuilder
{
    public class QueryBuilder
    {
        readonly ICollection<QueryItem> _queryItems;
        readonly string _indexName;

        public QueryBuilder(string indexName)
        {
            if (string.IsNullOrEmpty(indexName))
                throw new ArgumentException($"'{nameof(indexName)}' can't be null or empty.", nameof(indexName));

            _indexName = indexName;
            _queryItems = new List<QueryItem>();
        }

        public void Append(IndexQueryMap queryMap, bool isMandatory = true)
        {
            if (queryMap == null)
                throw new ArgumentNullException(nameof(queryMap));
            else if (!queryMap.ContainsKey(_indexName))
                throw new ArgumentException($"'{nameof(queryMap)}' not contains query for '{_indexName}'", nameof(queryMap));

            _queryItems.Append(new QueryItem(queryMap[_indexName], isMandatory));
        }

        public string BuildBoolQuery()
        {
            var mustQueries = _queryItems.Where(qItem => qItem.IsMandatory).SelectMany(GetMustQueriesOrDefault);
            var shouldQueries = _queryItems.Where(qItem => !qItem.IsMandatory).SelectMany(GetShouldQueriesOrDefault);

            return new JObject
            {
                ["bool"] = new JObject
                {
                    ["must"] = JArray.FromObject(mustQueries),
                    ["should"] = JArray.FromObject(shouldQueries),
                }
            }.ToString();
        }

        public string BuildAsAggregationByFieldQuery(string aggName,
                                                     string fieldName,
                                                     string fieldKey,
                                                     int size = 1000)
        {
            if (string.IsNullOrEmpty(aggName))
                throw new ArgumentException($"'{nameof(aggName)}' can't be null or empty.", nameof(aggName));

            if (string.IsNullOrEmpty(fieldName))
                throw new ArgumentException($"'{nameof(fieldName)}'  can't be null or empty.", nameof(fieldName));

            if (string.IsNullOrEmpty(fieldKey))
                throw new ArgumentException($"'{nameof(fieldKey)}' can't be null or empty.", nameof(fieldKey));

            var output = new JObject
            {
                ["size"] = 0, // Uses 0 because we don't need the documents of the index, only the buckets
                ["query"] = JObject.Parse(BuildBoolQuery()),
                ["aggs"] = new JObject
                {
                    [aggName] = new JObject
                    {
                        ["composite"] = new JObject
                        {
                            ["size"] = size,
                            ["source"] = JArray.FromObject(new[]
                            {
                                new JObject
                                {
                                    [fieldKey] = new JObject
                                    {
                                        ["terms"] = new JObject
                                        {
                                            ["field"] = fieldName
                                        }
                                    }
                                }
                            })
                        }
                    }
                }
            }.ToString();

            return output;
        }

        public string BuildAsSearchQuery(int page,
                                         int size,
                                         params (string fieldName, string order)[] orderBy)
        {
            var output = new JObject
            {
                ["from"] = page * size,
                ["size"] = size,
                ["query"] = JObject.Parse(BuildBoolQuery()),
                ["sort"] = JArray.FromObject(orderBy.Select(condition => new JObject
                {
                    [condition.fieldName] = condition.order
                }))
            }.ToString();

            return output;
        }

        static IEnumerable<JObject> GetMustQueriesOrDefault(QueryItem queryItem)
        {
            var mustQueries = JObject.Parse(queryItem.JsonQuery).SelectToken("bool.must");

            if (mustQueries == null)
                yield return JObject.Parse(queryItem.JsonQuery);
            else
            {
                foreach (var subQuery in mustQueries?.Children<JObject>() ?? Enumerable.Empty<JObject>())
                    yield return subQuery;
            }
        }

        static IEnumerable<JObject> GetShouldQueriesOrDefault(QueryItem queryItem)
        {
            var shouldQueries = JObject.Parse(queryItem.JsonQuery).SelectToken("bool.should");

            if (shouldQueries == null)
            {
                foreach (var query in GetMustQueriesOrDefault(queryItem))
                    yield return query;
            }
            else
            {
                foreach (var query in shouldQueries?.Children<JObject>() ?? Enumerable.Empty<JObject>())
                    yield return query;
            }
        }

    }
}
