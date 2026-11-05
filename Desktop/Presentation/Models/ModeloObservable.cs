using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Clarisa.Desktop.Presentation.Models;
public abstract class ModeloObservable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void Notificar([CallerMemberName] string? propiedad = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propiedad));
    protected bool Establecer<T>(ref T campo, T valor, [CallerMemberName] string? propiedad = null)
    {
        if (EqualityComparer<T>.Default.Equals(campo, valor))
            return false;
        campo = valor;
        Notificar(propiedad);
        return true;
    }
}
