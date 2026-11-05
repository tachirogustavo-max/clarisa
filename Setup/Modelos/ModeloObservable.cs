using System.ComponentModel;
using System.Runtime.CompilerServices;
namespace Clarisa.Setup.Modelos;
public abstract class ModeloObservable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void Notificar([CallerMemberName] string? nombre = null) => PropertyChanged?.Invoke(this, new(nombre));
    protected void Cambiar<T>(ref T campo, T valor, [CallerMemberName] string? nombre = null)
    {
        if (EqualityComparer<T>.Default.Equals(campo, valor)) return;
        campo = valor;
        Notificar(nombre);
    }
}
