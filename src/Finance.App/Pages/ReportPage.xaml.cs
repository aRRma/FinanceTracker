using Finance.Application.Features.Report;
using Finance.Application.Infrastructure;

namespace Finance.App.Pages;

/// <summary>Экраны E-01 и E-04: отчёт о расходах и доходах за месяц по группам.</summary>
public partial class ReportPage : DataPage
{
    private readonly ReportViewModel _model;

    /// <summary>Создаёт экран.</summary>
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
}
