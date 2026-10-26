namespace Catalog.Bll.DTOs;

public record ProductQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    string? ItemType = null,
    long? CategoryId = null,
    string SortBy = "name",
    string SortDir = "asc");
