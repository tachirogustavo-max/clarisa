namespace Clarisa.Desktop.Presentation.Models;
public interface ISalidaProtegida
{
    bool SolicitarSalida(Action continuar);
}

public sealed record EntradaArea(string Nombre, object Modelo);
public sealed class ModeloArea : ModeloObservable, ISalidaProtegida
{
    public ModeloArea(IEnumerable<EntradaArea> entradas)
    {
        Entradas = entradas.ToArray();
        if (Entradas.Count == 0)
            throw new ArgumentException("El área necesita al menos una vista.", nameof(entradas));
        Seleccionada = Entradas[0];
    }

    public IReadOnlyList<EntradaArea> Entradas { get; }
    public EntradaArea Seleccionada { get; private set; }

    public bool SolicitarSalida(Action continuar) => Seleccionada.Modelo is not ISalidaProtegida protector || protector.SolicitarSalida(continuar);
    public void Abrir(EntradaArea entrada)
    {
        if (!Entradas.Contains(entrada))
            return;
        if (SolicitarSalida(() => EstablecerEntrada(entrada)))
            EstablecerEntrada(entrada);
    }

    private void EstablecerEntrada(EntradaArea entrada)
    {
        Seleccionada = entrada;
        Notificar(nameof(Seleccionada));
    }

    public void Cancelar()
    {
        foreach (var entrada in Entradas)
        {
            if (entrada.Modelo is IListadoCatalogo listado)
                listado.Cancelar();
            if (entrada.Modelo is ModeloDirector director)
                director.Cancelar();
            if (entrada.Modelo is ModeloArea area)
                area.Cancelar();
            if (entrada.Modelo is ModeloVentanas ventanas)
                ventanas.Cancelar();
            if (entrada.Modelo is ModeloBoletines boletines)
                boletines.Cancelar();
            if (entrada.Modelo is ModeloFirmantes firmantes)
                firmantes.Cancelar();
        }
    }
}
