using Finance.Application.Features.Report;
using Finance.Application.Infrastructure;

namespace Finance.App.Pages;

/// <summary>
/// Экраны E-01 и E-04: отчёт о расходах и доходах за месяц по группам.
/// </summary>
public sealed partial class ReportPage : DataPage
{
    private readonly ReportViewModel _model;

    /// <summary>
    /// Создаёт экран.
    /// </summary>
    /// <param name="model">Модель представления отчёта.</param>
    /// <param name="startup">Подготовка приложения.</param>
    public ReportPage(ReportViewModel model, FinanceStartup startup)
        : base(startup)
    {
        ArgumentNullException.ThrowIfNull(model);

        InitializeComponent();

        _model = model;
        BindingContext = model;
    }

    /// <inheritdoc />
    protected override Task LoadAsync() => _model.LoadAsync();

    // Листание месяцев — команды модели: их доступность и защиту от второго нажатия
    // держит сама команда, а запуск из обработчика показывает сбой, а не роняет окно
    private void OnPreviousMonth(object? sender, TappedEventArgs e) => Guarded.Execute(_model.PreviousMonthCommand);

    private void OnNextMonth(object? sender, TappedEventArgs e) => Guarded.Execute(_model.NextMonthCommand);

    private void OnRowTapped(object? sender, TappedEventArgs e)
    {
        if (sender is BindableObject { BindingContext: ReportRowItem row })
        {
            Navigator.Go($"{Routes.ReportGroup}?key={row.Key}&month={_model.Month}");
        }
    }
}
