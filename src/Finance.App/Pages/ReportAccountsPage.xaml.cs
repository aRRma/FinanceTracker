using Finance.Application.Features.Report;
using Finance.Application.Infrastructure;

namespace Finance.App.Pages;

/// <summary>
/// Экран E-05: выбор счетов отчёта.
/// </summary>
public sealed partial class ReportAccountsPage : DataPage
{
    private readonly ReportAccountsViewModel _model;

    /// <summary>
    /// Создаёт экран.
    /// </summary>
    /// <param name="model">Модель представления выбора счетов отчёта.</param>
    /// <param name="startup">Подготовка приложения.</param>
    public ReportAccountsPage(ReportAccountsViewModel model, FinanceStartup startup)
        : base(startup)
    {
        ArgumentNullException.ThrowIfNull(model);

        InitializeComponent();

        _model = model;
        BindingContext = model;

        // Модель живёт ровно столько, сколько страница: отписываться не от чего
        model.Notified += Notice.Show;
    }

    /// <summary>
    /// Отметки читаются один раз, при открытии. Перечитывание при возврате из фона
    /// поставило бы их заново по выбору отчёта и стёрло бы снятые: пустые отметки
    /// для отчёта — умолчание, а на экране — шаг перехода на другую валюту.
    /// </summary>
    protected override bool ReloadsOnAppearing => false;

    /// <inheritdoc />
    protected override Task LoadAsync() => _model.LoadAsync();

    private void OnAccountTapped(object? sender, TappedEventArgs e)
    {
        if (sender is BindableObject { BindingContext: ReportAccountRow row })
        {
            _model.Toggle(row);
        }
    }

    private void OnAllActiveTapped(object? sender, TappedEventArgs e)
    {
        if (sender is BindableObject { BindingContext: ReportAccountsSection section })
        {
            _model.ChooseAllActive(section);
        }
    }
}
