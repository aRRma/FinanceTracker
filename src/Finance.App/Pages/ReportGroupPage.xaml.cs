using Finance.Application.Features.Report;
using Finance.Application.Infrastructure;

namespace Finance.App.Pages;

/// <summary>Экран E-02: подкатегории группы отчёта за месяц.</summary>
[QueryProperty(nameof(Key), "key")]
[QueryProperty(nameof(Month), "month")]
public partial class ReportGroupPage : DataPage
{
    private readonly ReportGroupViewModel _model;

    /// <summary>Создаёт экран.</summary>
    /// <param name="model">Модель представления группы отчёта.</param>
    /// <param name="startup">Подготовка приложения.</param>
    public ReportGroupPage(ReportGroupViewModel model, FinanceStartup startup)
        : base(startup)
    {
        ArgumentNullException.ThrowIfNull(model);

        InitializeComponent();

        _model = model;
        BindingContext = model;
    }

    /// <summary>Ключ группы из маршрута. Строкой, а не <see cref="Guid"/>: в маршруте он и есть строка.</summary>
    public string? Key { get; set; }

    /// <summary>Месяц отчёта из маршрута в виде «2026-08».</summary>
    public string? Month { get; set; }

    /// <inheritdoc />
    protected override Task LoadAsync() =>
        Guid.TryParse(Key, out Guid key) && ReportMonth.TryParse(Month, out ReportMonth month)
            ? _model.LoadAsync(key, month)
            : Task.CompletedTask;

    private void OnRowTapped(object? sender, TappedEventArgs e)
    {
        if (sender is BindableObject { BindingContext: ReportRowItem row })
        {
            Guarded.Run(() => Shell.Current.GoToAsync($"{Routes.ReportSubcategory}?key={row.Key}&month={Month}"));
        }
    }
}
