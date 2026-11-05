using System.ComponentModel;

namespace Clarisa.Desktop.Presentation.Models;
public sealed class LapsoVisual(Guid id, string nombre, int orden, bool actual, bool cargaAbierta, long version) : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public Guid Id { get; } = id;
    public string Nombre { get; } = nombre;
    public int Orden { get; } = orden;
    public bool Actual { get; } = actual;
    public bool CargaAbierta { get; private set; } = cargaAbierta;
    public long Version { get; private set; } = version;
    public bool SinConfirmar { get; private set; }
    public string Estado => SinConfirmar ? "Estado por comprobar" : CargaAbierta ? "Carga abierta" : "Carga cerrada";
    public string Accion => CargaAbierta ? "Cerrar carga" : "Abrir carga";

    public void Actualizar(bool abierta, long revision)
    {
        CargaAbierta = abierta;
        Version = revision;
        SinConfirmar = false;
        Notificar();
    }

    public void MarcarSinConfirmar()
    {
        SinConfirmar = true;
        Notificar();
    }

    private void Notificar()
    {
        foreach (var nombre in new[]
        {
            nameof(CargaAbierta),
            nameof(Version),
            nameof(SinConfirmar),
            nameof(Estado),
            nameof(Accion)
        }

        )
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nombre));
    }
}
