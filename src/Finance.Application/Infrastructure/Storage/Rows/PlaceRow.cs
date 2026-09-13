namespace Finance.Application.Infrastructure.Storage.Rows;

/// <summary>Строка справочника мест.</summary>
internal sealed class PlaceRow : EntityRow
{
    /// <summary>Название места.</summary>
    public required string Name { get; set; }
}
