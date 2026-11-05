using Clarisa.Desktop.Services;

namespace Clarisa.Desktop.Presentation.Models;
public sealed class ComparadorNotas : ModeloObservable
{
    private readonly IClienteLecturas? cliente;
    private readonly ContextoPlanilla contexto;
    private readonly Func<IReadOnlyList<FilaNota>> locales;
    private readonly Action<IReadOnlyList<FilaNota>, bool> aplicar;
    private CancellationTokenSource? consulta;
    private PlanillaRemota? leida;
    public ComparadorNotas(IClienteLecturas? cliente, ContextoPlanilla contexto, Func<IReadOnlyList<FilaNota>> locales, Action<IReadOnlyList<FilaNota>, bool> aplicar)
    {
        this.cliente = cliente;
        this.contexto = contexto;
        this.locales = locales;
        this.aplicar = aplicar;
    }

    public bool Visible { get; private set; }
    public bool Cargando { get; private set; }
    public string Mensaje { get; private set; } = "";
    public IReadOnlyList<FilaComparacionNota> Filas { get; private set; } = [];

    public async Task ConsultarAsync()
    {
        if (Cargando)
            return;
        Visible = true;
        Mensaje = "";
        leida = null;
        Filas = [];
        if (cliente is null)
        {
            Mensaje = "Conecta con el servidor para comparar las notas.";
            Actualizar();
            return;
        }

        using var actual = new CancellationTokenSource();
        consulta = actual;
        Cargando = true;
        Actualizar();
        try
        {
            var respuesta = await cliente.ConsultarAsync<PlanillaRemota>("api/calificaciones?seccionId=" + contexto.SeccionId + "&materiaId=" + contexto.MateriaId + "&lapsoId=" + contexto.LapsoId, actual.Token);
            if (actual.IsCancellationRequested)
                return;
            if (!ValidacionPlanillaRemota.EsValida(respuesta.Datos, contexto))
            {
                Mensaje = respuesta.Error ?? "No llegó una planilla válida para comparar.";
                return;
            }

            leida = respuesta.Datos!;
            var remotas = leida.Filas.ToDictionary(f => f.InscripcionId);
            Filas = locales().Where(f => f.Cambiada).Select(f => new FilaComparacionNota(f, remotas.GetValueOrDefault(f.InscripcionId), leida)).ToArray();
        }
        catch (OperationCanceledException)when (actual.IsCancellationRequested)
        {
        }
        finally
        {
            Cargando = false;
            consulta = null;
            Actualizar();
        }
    }

    public void Aplicar()
    {
        if (Cargando || leida is null)
            return;
        if (Filas.Any(f => f.Decision is null))
        {
            Mensaje = "Elige qué conservar en cada fila antes de continuar.";
            Actualizar();
            return;
        }

        var decisiones = Filas.ToDictionary(f => f.Local.InscripcionId);
        var nuevas = leida.Filas.Select(remota =>
        {
            var fila = FabricaFilasNota.Crear(leida, remota);
            if (decisiones.TryGetValue(remota.InscripcionId, out var comparada) && comparada.Decision?.Valor == DecisionNota.ConservarLocal)
            {
                fila.Texto = comparada.Local.Texto;
                fila.Letra = comparada.Local.Letra;
            }
            return fila;
        }).ToArray();
        aplicar(nuevas, leida.CargaPermitida);
        Visible = false;
        Actualizar();
    }

    public void Ocultar()
    {
        consulta?.Cancel();
        Visible = false;
        leida = null;
        Filas = [];
        Actualizar();
    }

    private void Actualizar()
    {
        foreach (var campo in new[]
        {
            nameof(Visible),
            nameof(Cargando),
            nameof(Mensaje),
            nameof(Filas)
        }

        )
            Notificar(campo);
    }
}
