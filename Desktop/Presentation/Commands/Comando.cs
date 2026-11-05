using System.Windows.Input;

namespace Clarisa.Desktop.Presentation.Commands;
public sealed class Comando(Action<object?> ejecutar, Predicate<object?>? permitido = null) : ICommand
{
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => permitido?.Invoke(parameter) ?? true;
    public void Execute(object? parameter)
    {
        if (CanExecute(parameter))
            ejecutar(parameter);
    }

    public void Actualizar() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
