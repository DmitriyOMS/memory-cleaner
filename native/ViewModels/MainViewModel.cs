using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using MemoryCleaner.Models;
using MemoryCleaner.Services;

namespace MemoryCleaner.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly ProcessService _processService;
    private string _searchText = string.Empty;
    private bool _isBusy;
    private string _memoryText = "ОЗУ: загрузка…";
    private string _statusText = "Подготовка списка процессов…";
    private string _selectionText = "Ничего не выбрано";
    private bool _hasResults;

    public MainViewModel(ProcessService processService)
    {
        _processService = processService;
        AppsView = CollectionViewSource.GetDefaultView(Apps);
        AppsView.Filter = FilterApplication;
        AppsView.SortDescriptions.Add(new SortDescription(nameof(ProcessGroup.MemoryBytes), ListSortDirection.Descending));

        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsBusy);
        SelectSafeCommand = new RelayCommand(SelectWindowedApplications, () => !IsBusy);
        ClearSelectionCommand = new RelayCommand(ClearSelection, () => !IsBusy && Apps.Any(app => app.IsSelected));
        UnloadCommand = new AsyncRelayCommand(UnloadSelectedAsync, () => !IsBusy && Apps.Any(app => app.IsSelected && app.CanUnload));
    }

    public ObservableCollection<ProcessGroup> Apps { get; } = new();
    public ICollectionView AppsView { get; }
    public ObservableCollection<UnloadResult> Results { get; } = new();

    public AsyncRelayCommand RefreshCommand { get; }
    public RelayCommand SelectSafeCommand { get; }
    public RelayCommand ClearSelectionCommand { get; }
    public AsyncRelayCommand UnloadCommand { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
                AppsView.Refresh();
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(IsNotBusy));
                RaiseCommandStates();
            }
        }
    }

    public bool IsNotBusy => !IsBusy;

    public string MemoryText
    {
        get => _memoryText;
        private set => SetProperty(ref _memoryText, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string SelectionText
    {
        get => _selectionText;
        private set => SetProperty(ref _selectionText, value);
    }

    public bool HasResults
    {
        get => _hasResults;
        private set => SetProperty(ref _hasResults, value);
    }

    public Task InitializeAsync() => RefreshAsync();

    private async Task RefreshAsync()
    {
        if (IsBusy)
            return;

        IsBusy = true;
        StatusText = "Обновление списка процессов…";
        try
        {
            await RefreshSnapshotAsync();
            StatusText = $"Приложений: {Apps.Count} · обновлено {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            StatusText = "Не удалось обновить список процессов";
            ShowError(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RefreshSnapshotAsync()
    {
        var snapshot = await _processService.GetSnapshotAsync();

        foreach (var app in Apps)
            app.PropertyChanged -= OnAppPropertyChanged;
        Apps.Clear();

        foreach (var app in snapshot.Applications)
        {
            app.IsSelected = false;
            app.PropertyChanged += OnAppPropertyChanged;
            Apps.Add(app);
        }

        MemoryText = snapshot.Memory.TotalBytes == 0
            ? "Показатели ОЗУ недоступны"
            : $"ОЗУ: {snapshot.Memory.UsedGB:N1} ГБ занято из {snapshot.Memory.TotalGB:N1} ГБ · свободно {snapshot.Memory.AvailableGB:N1} ГБ";
        UpdateSelection();
    }

    private bool FilterApplication(object item)
    {
        if (item is not ProcessGroup app || string.IsNullOrWhiteSpace(SearchText))
            return true;

        return app.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
               app.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
               app.Company.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
    }

    private void SelectWindowedApplications()
    {
        foreach (var app in Apps.Where(app => app.CanUnload && app.HasWindow))
            app.IsSelected = true;
        UpdateSelection();
    }

    private void ClearSelection()
    {
        foreach (var app in Apps)
            app.IsSelected = false;
        UpdateSelection();
    }

    private async Task UnloadSelectedAsync()
    {
        var selected = Apps.Where(app => app.IsSelected && app.CanUnload).ToArray();
        if (selected.Length == 0 || IsBusy)
            return;

        var names = string.Join("\n", selected.Take(12).Select(app => $"• {app.Name}"));
        if (selected.Length > 12)
            names += $"\n• и ещё {selected.Length - 12}";
        var memory = selected.Sum(app => app.MemoryMB);
        var answer = MessageBox.Show(
            $"Будут закрыты приложения ({selected.Length}):\n{names}\n\n" +
            $"Ожидаемое освобождение: примерно {memory:N0} МБ.\n" +
            "Несохранённые данные могут быть потеряны. Продолжить?",
            "Подтверждение выгрузки",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (answer != MessageBoxResult.Yes)
            return;

        IsBusy = true;
        StatusText = "Выгрузка выбранных приложений…";
        try
        {
            var results = await _processService.UnloadAsync(selected.Select(app => app.Name));
            Results.Clear();
            foreach (var result in results)
                Results.Add(result);
            HasResults = Results.Count > 0;

            await RefreshSnapshotAsync();
            var completed = results.Count(result => result.Status == UnloadStatus.Completed);
            StatusText = $"Операция завершена: успешно {completed} из {results.Count}";
        }
        catch (Exception ex)
        {
            StatusText = "Не удалось завершить операцию";
            ShowError(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OnAppPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ProcessGroup.IsSelected))
            UpdateSelection();
    }

    private void UpdateSelection()
    {
        var selected = Apps.Where(app => app.IsSelected && app.CanUnload).ToArray();
        SelectionText = selected.Length == 0
            ? "Ничего не выбрано"
            : $"Выбрано: {selected.Length} · примерно {selected.Sum(app => app.MemoryMB):N0} МБ";
        RaiseCommandStates();
    }

    private void RaiseCommandStates()
    {
        RefreshCommand.RaiseCanExecuteChanged();
        SelectSafeCommand.RaiseCanExecuteChanged();
        ClearSelectionCommand.RaiseCanExecuteChanged();
        UnloadCommand.RaiseCanExecuteChanged();
    }

    private static void ShowError(Exception exception)
    {
        MessageBox.Show(
            exception.Message,
            "Ошибка",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}
