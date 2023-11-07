﻿using ElasticSearchQueryBuilder.Models;
using Newtonsoft.Json.Linq;

namespace ElasticSearchQueryBuilder
{
    public class ElasticQueryBuilder
    {
        public string IndexName { get; }
        public ICollection<ElasticQueryItem> Queries { get; }
        public bool EvaluateAsOr { get; }

        public ElasticQueryBuilder(string indexName, bool evaluateAsOr = false)
        {
            if (string.IsNullOrEmpty(indexName))
                throw new ArgumentException($"'{nameof(indexName)}' can't be null or empty.", nameof(indexName));

            IndexName = indexName;
            EvaluateAsOr = evaluateAsOr;
            Queries = new List<ElasticQueryItem>();
        }

        public void Append(ElasticIndexQueryMap queryMap, bool isMandatory = true)
        {
            if (queryMap == null)
                throw new ArgumentNullException(nameof(queryMap));
            else if (!queryMap.ContainsKey(IndexName))
                throw new ArgumentException($"'{nameof(queryMap)}' not contains query for '{IndexName}'", nameof(queryMap));

            Queries.Append(new ElasticQueryItem(queryMap[IndexName], isMandatory));
        }

        public string GetQueryForAggregationByField(string aggName,
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

            var mustQueries = Queries.Where(qItem => qItem.IsMandatory).SelectMany(GetMustQueriesOrDefault);
            var shouldQueries = Queries.Where(qItem => !qItem.IsMandatory).SelectMany(GetShouldQueriesOrDefault);

            var output = new JObject
            {
                ["size"] = 0, // Uses 0 because we don't need the documents of the index, only the buckets
                ["query"] = new JObject
                {
                    ["bool"] = new JObject
                    {
                        ["must"] = JArray.FromObject(mustQueries),
                        ["should"] = JArray.FromObject(shouldQueries),
                    }
                },
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

        public string GetQueryForSearch(int page,
                                        int size,
                                        params (string fieldName, string order)[] orderBy)
        {
            var mustQueries = Queries.Where(qItem => qItem.IsMandatory).SelectMany(GetMustQueriesOrDefault);
            var shouldQueries = Queries.Where(qItem => !qItem.IsMandatory).SelectMany(GetShouldQueriesOrDefault);

            var output = new JObject
            {
                ["from"] = page * size,
                ["size"] = size,
                ["query"] = new JObject
                {
                    ["bool"] = new JObject
                    {
                        ["must"] = JArray.FromObject(mustQueries),
                        ["should"] = JArray.FromObject(shouldQueries),
                    }
                },
                ["sort"] = JArray.FromObject(orderBy.Select(condition => new JObject
                {
                    [condition.fieldName] = condition.order
                }))
            }.ToString();

            return output;
        }

        static IEnumerable<string> GetMustQueriesOrDefault(ElasticQueryItem queryItem)
        {
            var mustQueries = JObject.Parse(queryItem.JsonQuery).SelectToken("bool.must");

            if (mustQueries == null)
                yield return queryItem.JsonQuery;
            else
            {
                foreach (var subQuery in mustQueries?.Children<JObject>() ?? Enumerable.Empty<JObject>())
                    yield return subQuery.ToString();
            }
        }

        static IEnumerable<string> GetShouldQueriesOrDefault(ElasticQueryItem queryItem)
        {
            var shouldQueries = JObject.Parse(queryItem.JsonQuery).SelectToken("bool.should");

            if (shouldQueries == null)
            {
                foreach (var subQuery in GetMustQueriesOrDefault(queryItem))
                    yield return subQuery.ToString();
            }
            else
            {
                foreach (var subQuery in shouldQueries?.Children<JObject>() ?? Enumerable.Empty<JObject>())
                    yield return subQuery.ToString();
            }
        }

    }
}