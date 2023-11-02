﻿using ElasticSearchQueryBuilder.Enums;
using Newtonsoft.Json.Linq;

namespace ElasticSearchQueryBuilder.Models.Abstracts
{
    internal abstract class ElasticFilter
    {
        public Filter Specification { get; }

        public bool IsMustAssert => new FilterOperator[]
        {
            FilterOperator.IsNot,
            FilterOperator.NotContains,
            FilterOperator.NotBetween,
        }.Contains(Specification.Operator);

        public abstract JObject Query { get; init; }

        public ElasticFilter(Filter specification)
        {
            if (string.IsNullOrEmpty(specification.IndexName))
                throw new ArgumentException($"'{nameof(specification.IndexName)}' can't be null or empty.", nameof(specification.IndexName));

            if (string.IsNullOrEmpty(specification.FieldName))
                throw new ArgumentException($"'{nameof(specification.FieldName)}' can't be null or empty.", nameof(specification.FieldName));

            if (specification.Values == null || !specification.Values.Any())
                throw new ArgumentException($"'{nameof(specification.Values)}' can't be null or empty.", nameof(specification.Values));

            if (string.IsNullOrEmpty(specification.NestedOf))
                specification.NestedOf = null;

            Specification = specification;
        }

        public override string? ToString() => Query?.ToString();
    }
}