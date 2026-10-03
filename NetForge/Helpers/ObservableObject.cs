using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace NetForge.Helpers;

public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name)); return true;
    }
    protected void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class RelayCommand : ICommand
{
    private readonly Func<Task> _run; private readonly Func<bool>? _can;
    public RelayCommand(Func<Task> run, Func<bool>? can = null) { _run = run; _can = can; }
    public RelayCommand(Action run, Func<bool>? can = null) : this(() => { run(); return Task.CompletedTask; }, can) { }
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? p) => _can?.Invoke() ?? true;
    public async void Execute(object? p) { try { await _run(); } catch (OperationCanceledException) { } catch (Exception ex) { Services.Logger.Write(ex); } }
    public void Refresh() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
