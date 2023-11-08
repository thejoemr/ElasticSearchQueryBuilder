using ElasticSearchQueryBuilder.Enums;

namespace ElasticSearchQueryBuilder.Models;

public class FilterSpecification
{
    public string IndexName { get; set; } = null!;
    public string FieldName { get; set; } = null!;
    public string? NestedOf { get; set; }
    public FilterOperator Operator { get; set; }
    public FilterType Type { get; set; }
    public IEnumerable<string> Values { get; set; } = Enumerable.Empty<string>();
}
