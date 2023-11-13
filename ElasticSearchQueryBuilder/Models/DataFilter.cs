using ElasticSearchQueryBuilder.Enums;
using ElasticSearchQueryBuilder.Models.Abstracts;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;

namespace ElasticSearchQueryBuilder.Models
{
    internal class DataFilter : Filter
    {
        private bool IsExact => new FilterOperator[]
        {
            FilterOperator.Is,
            FilterOperator.IsNot
        }.Contains(Specification.Operator);

        private bool IsBetween => new FilterOperator[]
        {
            FilterOperator.Between,
            FilterOperator.NotBetween,
        }.Contains(Specification.Operator);

        private bool IsLessThan => new FilterOperator[]
        {
            FilterOperator.LessThan
        }.Contains(Specification.Operator);

        private bool IsGreaterThan => new FilterOperator[]
        {
            FilterOperator.GreaterThan
        }.Contains(Specification.Operator);

        public override JObject Query { get; protected set; }

        private const string NUMBER_REGEX_PATTERN = @"[_]?\d+[.]?\d*";
        private const string DATE_REGEX_PATTERN = @"[_]?(\d{4})-(0[1-9]|1[0-2])-(0[1-9]|[12][0-9]|3[01])";

        public DataFilter(FilterSpecification specification) : base(specification)
        {
            if (!new FilterType[]
            {
                FilterType.Number,
                FilterType.Date
            }.Contains(specification.Type))
                throw new ArgumentException($"This is not a correct object for a '{specification.Type}' filter");

            if (new FilterOperator[]
            {
                FilterOperator.Contains,
                FilterOperator.NotContains
            }.Contains(specification.Operator))
                throw new ArgumentException($"The operator '{specification.Operator}' is not valid for DateTime filters");

            if (!IsBetween)
            {
                if (specification.Type == FilterType.Number && specification.Values.Any(value => !Regex.IsMatch(value, $"^{NUMBER_REGEX_PATTERN}$")))
                    throw new ArgumentException(@$"Some values doesn't match with the mask
                                               each value must be a number and optionaly includes '_' at start
                                               to indicate that the value will be included to evaluate");

                if (specification.Type == FilterType.Date && specification.Values.Any(value => !Regex.IsMatch(value, $"^{DATE_REGEX_PATTERN}$")))
                    throw new ArgumentException(@$"Some values doesn't match with the mask
                                               each value must be a date of type 'yyyy-MM-dd' and optionaly includes '_' at start
                                               to indicate that the value will be included to evaluate");
            }
            else
            {
                if (specification.Type == FilterType.Number && specification.Values.Any(value => !Regex.IsMatch(value, @$"^({NUMBER_REGEX_PATTERN})[;]({NUMBER_REGEX_PATTERN})$")))
                    throw new ArgumentException(@$"Some values doesn't match with the mask
                                               '<lowlimit>;<upperlimit>' (1;10 => > 1 and < 10), 
                                               and optionaly includes '_' at start of each value 
                                               to indicate that the value will be included to evaluate (1;_10 => > 1 and <= 10)");

                if (specification.Type == FilterType.Date && specification.Values.Any(value => !Regex.IsMatch(value, @$"^({DATE_REGEX_PATTERN})[;]({DATE_REGEX_PATTERN})$")))
                    throw new ArgumentException(@$"Some values doesn't match with the mask
                                               '<lowlimit>;<upperlimit>' (1997-01-01;2010-01-01 => > 1997-01-01 and < 2010-01-01), 
                                               and optionaly includes '_' at start of each value 
                                               to indicate that the value will be included to evaluate (1997-01-01;_2010-01-01 => > > 1997-01-01 and <= 2010-01-01)");
            }

            Query = GetQuery();
        }

        private IEnumerable<JObject> GetExactQuery() => Specification.Values.Select(value => new JObject()
        {
            ["range"] = new JObject
            {
                [Specification.FieldName] = new JObject
                {
                    [value.StartsWith("_") ? "gte" : "gt"] = value.Replace("_", string.Empty).Trim(),
                    [value.StartsWith("_") ? "lte" : "lt"] = value.Replace("_", string.Empty).Trim()
                }
            }
        });

        private IEnumerable<JObject> GetBetweenQuery() => Specification.Values.Select(value =>
        {
            var lowLimit = value.Split(';').First();
            var upperLimit = value.Split(';').Last();

            var query = new JObject()
            {
                ["range"] = new JObject
                {
                    [Specification.FieldName] = new JObject
                    {
                        [lowLimit.StartsWith("_") ? "gte" : "gt"] = lowLimit.Replace("_", string.Empty).Trim(),
                        [upperLimit.StartsWith("_") ? "lte" : "lt"] = upperLimit.Replace("_", string.Empty).Trim()
                    }
                }
            };

            return query;
        });

        private IEnumerable<JObject> GetGreaterThanQuery() => Specification.Values.Select(value => new JObject
        {
            ["range"] = new JObject
            {
                [Specification.FieldName] = new JObject
                {
                    [value.StartsWith("_") ? "gte" : "gt"] = value.Replace("_", string.Empty).Trim()
                }
            }
        });

        private IEnumerable<JObject> GetLessThanQuery() => Specification.Values.Select(value => new JObject
        {
            ["range"] = new JObject
            {
                [Specification.FieldName] = new JObject
                {
                    [value.StartsWith("_") ? "lte" : "lt"] = value.Replace("_", string.Empty).Trim()
                }
            }
        });

        private JObject GetQuery()
        {
            IEnumerable<JObject> queries;

            if (IsExact)
                queries = GetExactQuery();
            else if (IsBetween)
                queries = GetBetweenQuery();
            else if (IsGreaterThan)
                queries = GetGreaterThanQuery();
            else if (IsLessThan)
                queries = GetLessThanQuery();
            else
                throw new NotImplementedException($"Filter is not cofigured correctly");

            return queries.Count() <= 1
            ? queries.First()
            : new JObject
            {
                ["bool"] = new JObject
                {
                    ["should"] = JArray.FromObject(queries)
                }
            };
        }
    }
}
