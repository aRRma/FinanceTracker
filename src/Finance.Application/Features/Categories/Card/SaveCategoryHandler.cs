using Finance.Application.Infrastructure;
using Finance.Application.Infrastructure.Storage;
using Finance.Application.Infrastructure.Storage.Rows;
using Finance.Domain.Entities;
using Finance.Domain.Enums;
using Finance.Domain.Rules;
using Microsoft.EntityFrameworkCore;

namespace Finance.Application.Features.Categories.Card;

/// <summary>
/// Заводит и правит категории обоих уровней. Соседей по имени, группу и её
/// содержимое читает здесь и подаёт домену параметром: решение остаётся за доменом,
/// а в хранилище он не ходит.
/// </summary>
public sealed class SaveCategoryHandler : ISaveCategoryHandler
{
    /// <summary>
    /// Имя приёмника. Совпадает с именем приёмников стартового набора: группа,
    /// заведённая руками, обязана выглядеть так же, как наборная.
    /// </summary>
    private const string ReceiverName = "Прочее";

    private readonly UnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly IconCatalog _icons;

    /// <summary>
    /// Создаёт обработчик.
    /// </summary>
    /// <param name="unitOfWork">Граница транзакции.</param>
    /// <param name="clock">Часы: момент записи.</param>
    /// <param name="icons">Набор значков: запасным помечается приёмник.</param>
    public SaveCategoryHandler(UnitOfWork unitOfWork, IClock clock, IconCatalog icons)
    {
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(icons);

        _unitOfWork = unitOfWork;
        _clock = clock;
        _icons = icons;
    }

    /// <inheritdoc />
    public Task<Guid> HandleAsync(SaveCategoryCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        return _unitOfWork.ExecuteAsync<Guid>(
            async (context, token) =>
            {
                Guid key = command.Key is { } existing
                    ? await UpdateAsync(context, command, existing, token).ConfigureAwait(false)
                    : await CreateAsync(context, command, token).ConfigureAwait(false);

                await context.SaveChangesAsync(token).ConfigureAwait(false);

                return (key, DataChange.Categories);
            },
            cancellationToken);
    }

    private Task<Guid> CreateAsync(
        FinanceDbContext context,
        SaveCategoryCommand command,
        CancellationToken cancellationToken) =>
        command.ParentKey is { } parentKey
            ? CreateSubcategoryAsync(context, command, parentKey, cancellationToken)
            : CreateGroupAsync(context, command, cancellationToken);

    private async Task<Guid> CreateGroupAsync(
        FinanceDbContext context,
        SaveCategoryCommand command,
        CancellationToken cancellationToken)
    {
        CategoryKind kind = command.Kind
                            ?? throw new ArgumentException("Вид заводимой группы обязателен", nameof(command));

        await EnsureGroupNameFreeAsync(context, command.Name, kind, self: null, cancellationToken)
            .ConfigureAwait(false);

        Category group = Category.CreateGroup(command.Name, kind, command.Icon, _clock.NowUtc);

        // Приёмник заводится здесь же, а не следующей командой: между двумя
        // командами группа успела бы остаться без «Прочего», и удаление
        // подкатегории в ней падало бы навсегда
        Category receiver = Category.CreateSubcategory(
            group, ReceiverName, _icons.Fallback, _clock.NowUtc, CategoryRole.Other);

        context.Categories.AddRange(group.ToRow(), receiver.ToRow());

        return group.Key;
    }

    private async Task<Guid> CreateSubcategoryAsync(
        FinanceDbContext context,
        SaveCategoryCommand command,
        Guid parentKey,
        CancellationToken cancellationToken)
    {
        Category parent = await ReadGroupAsync(context, parentKey, cancellationToken).ConfigureAwait(false);

        IReadOnlyList<Category> siblings = await ReadSubcategoriesAsync(context, parentKey, cancellationToken)
            .ConfigureAwait(false);

        await EnsureSubcategoryNameFreeAsync(context, command.Name, parent, self: null, cancellationToken)
            .ConfigureAwait(false);

        Category created = Category.CreateSubcategory(parent, command.Name, command.Icon, _clock.NowUtc);

        // Состав группы проверяется с новой подкатегорией: в служебную группу
        // вторая не влезает, и поймать это нужно до записи — потом приёмник
        // в ней перестал бы находиться, и удалять в ней стало бы нечем
        CategoryRules.EnsureHasReceiver(parent, [.. siblings, created]);

        context.Categories.Add(created.ToRow());

        return created.Key;
    }

    private async Task<Guid> UpdateAsync(
        FinanceDbContext context,
        SaveCategoryCommand command,
        Guid key,
        CancellationToken cancellationToken)
    {
        CategoryRow row = await context.Categories
            .FirstOrDefaultAsync(existing => existing.Key == key, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Категория {key} не найдена");

        Category category = row.ToDomain();

        if (category.IsGroup)
        {
            await RenameGroupAsync(context, command, category, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await UpdateSubcategoryAsync(context, command, category, cancellationToken).ConfigureAwait(false);
        }

        category.ChangeIcon(command.Icon);
        category.CopyTo(row);

        return key;
    }

    private static async Task RenameGroupAsync(
        FinanceDbContext context,
        SaveCategoryCommand command,
        Category group,
        CancellationToken cancellationToken)
    {
        CategoryKind kind = group.Kind
                            ?? throw new InvalidOperationException($"У группы «{group.Name}» не задан вид");

        await EnsureGroupNameFreeAsync(context, command.Name, kind, group.Key, cancellationToken)
            .ConfigureAwait(false);

        group.Rename(command.Name);
    }

    private static async Task UpdateSubcategoryAsync(
        FinanceDbContext context,
        SaveCategoryCommand command,
        Category subcategory,
        CancellationToken cancellationToken)
    {
        Guid currentKey = subcategory.ParentKey!.Value;
        Guid targetKey = command.ParentKey ?? currentKey;

        Category target = await ReadGroupAsync(context, targetKey, cancellationToken).ConfigureAwait(false);

        // Проверка и переименование выполняются всегда, а не только при изменении
        // имени по существу: сравнение уникальности не различает регистр, и правка
        // «такси» на «Такси» иначе молча не сохранялась бы
        await EnsureSubcategoryNameFreeAsync(context, command.Name, target, subcategory.Key, cancellationToken)
            .ConfigureAwait(false);

        subcategory.Rename(command.Name);

        if (targetKey == currentKey)
        {
            return;
        }

        Category current = await ReadGroupAsync(context, currentKey, cancellationToken).ConfigureAwait(false);

        IReadOnlyList<Category> neighbours = await ReadSubcategoriesAsync(context, targetKey, cancellationToken)
            .ConfigureAwait(false);

        subcategory.MoveTo(current, target, neighbours.Select(static neighbour => neighbour.Name));
    }

    /// <summary>
    /// Имя группы уникально среди групп своего вида: доходная и расходная «Еда» сосуществуют.
    /// </summary>
    private static async Task EnsureGroupNameFreeAsync(
        FinanceDbContext context,
        string name,
        CategoryKind kind,
        Guid? self,
        CancellationToken cancellationToken)
    {
        var taken = await context.Categories
            .AsNoTracking()
            .Where(row => row.ParentKey == null && row.Kind == kind)
            .Select(row => new { row.Key, row.Name })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        string what = kind is CategoryKind.Expense ? "группа расходов" : "группа доходов";

        if (self is { } key)
        {
            NameUniqueness.EnsureForRename(
                name,
                taken.Select(static group => (group.Key, group.Name)),
                key,
                what);
        }
        else
        {
            NameUniqueness.Ensure(name, taken.Select(static group => group.Name), what);
        }
    }

    /// <summary>
    /// Имя подкатегории уникально внутри группы; при переносе — внутри целевой.
    /// </summary>
    private static async Task EnsureSubcategoryNameFreeAsync(
        FinanceDbContext context,
        string name,
        Category parent,
        Guid? self,
        CancellationToken cancellationToken)
    {
        var taken = await context.Categories
            .AsNoTracking()
            .Where(row => row.ParentKey == parent.Key)
            .Select(row => new { row.Key, row.Name })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        string what = $"подкатегория группы «{parent.Name}»";

        if (self is { } key)
        {
            NameUniqueness.EnsureForRename(
                name,
                taken.Select(static subcategory => (subcategory.Key, subcategory.Name)),
                key,
                what);
        }
        else
        {
            NameUniqueness.Ensure(name, taken.Select(static subcategory => subcategory.Name), what);
        }
    }

    private static async Task<Category> ReadGroupAsync(
        FinanceDbContext context,
        Guid key,
        CancellationToken cancellationToken)
    {
        CategoryRow row = await context.Categories
            .AsNoTracking()
            .FirstOrDefaultAsync(group => group.Key == key, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Группа {key} не найдена");

        return row.ToDomain();
    }

    private static async Task<IReadOnlyList<Category>> ReadSubcategoriesAsync(
        FinanceDbContext context,
        Guid parentKey,
        CancellationToken cancellationToken)
    {
        List<CategoryRow> rows = await context.Categories
            .AsNoTracking()
            .Where(row => row.ParentKey == parentKey)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. rows.Select(static row => row.ToDomain())];
    }
}
