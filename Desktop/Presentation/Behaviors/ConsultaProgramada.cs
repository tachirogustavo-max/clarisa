using System.Windows.Threading;
using Clarisa.Desktop.Presentation.Models;

namespace Clarisa.Desktop.Presentation.Behaviors;

internal sealed class ConsultaProgramada : IDisposable
{
    private readonly DispatcherTimer espera;
    private readonly Func<IListadoCatalogo?> vigente;
    public ConsultaProgramada(Dispatcher dispatcher, Func<IListadoCatalogo?> vigente, TimeSpan? demora = null)
    {
        this.vigente = vigente;
        espera = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
        {
            Interval = demora ?? TimeSpan.FromMilliseconds(350)
        };
        espera.Tick += Consultar;
    }
    public void Programar() { espera.Stop(); espera.Start(); }
    public void Detener() => espera.Stop();
    private async void Consultar(object? sender, EventArgs e)
    {
        espera.Stop();
        if (vigente() is not { EditorAbierto: false, FiltroPendiente: true } listado) return;
        if (listado.Cargando) { espera.Start(); return; }
        await listado.BuscarAsync();
    }
    public void Dispose()
    {
        espera.Stop();
        espera.Tick -= Consultar;
    }
}
