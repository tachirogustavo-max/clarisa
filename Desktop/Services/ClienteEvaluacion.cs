using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Clarisa.Desktop.Services.Catalogos;

namespace Clarisa.Desktop.Services;

public sealed record LetraRemota(Guid Id, Guid AnioLectivoId, string Letra, string? Descripcion, long Version);
public sealed record PlanMateriaRemota(Guid Id, Guid AnioLectivoId, string Grado, Guid MateriaId, int Modalidad, long Version)
{
    public string Materia { get; init; } = "";
    public string Evaluacion => Modalidad switch
    {
        ModalidadRemota.NoCursada => "No cursada",
        ModalidadRemota.Numerica => "Cuantitativa",
        ModalidadRemota.Cualitativa => "Cualitativa",
        ModalidadRemota.NumericaYCualitativa => "Nota y letra",
        _ => "Sin configurar"
    };
}

public sealed class ClienteEvaluacion(HttpClient transporte, Uri servidor)
{
    public async Task<ResultadoRemoto<T>> EnviarAsync<T>(HttpMethod metodo, string ruta, object? datos, CancellationToken ct)
    {
        if (!ruta.StartsWith("api/", StringComparison.Ordinal) || ruta.Contains("..", StringComparison.Ordinal)
            || DireccionServidor.Validar(servidor.AbsoluteUri).Error is not null) throw new ArgumentException("Destino de evaluación no válido.");
        using var espera = CancellationTokenSource.CreateLinkedTokenSource(ct);
        espera.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            using var solicitud = new HttpRequestMessage(metodo, new Uri(servidor, ruta));
            if (datos is not null) solicitud.Content = JsonContent.Create(datos);
            using var respuesta = await transporte.SendAsync(solicitud, HttpCompletionOption.ResponseHeadersRead, espera.Token);
            await respuesta.Content.LoadIntoBufferAsync(64 * 1024, espera.Token);
            if (!respuesta.IsSuccessStatusCode) return await RechazoCatalogo.LeerAsync<T>(respuesta, espera.Token);
            if (respuesta.StatusCode == HttpStatusCode.NoContent) return new(default, null);
            var confirmado = await respuesta.Content.ReadFromJsonAsync<T>(espera.Token);
            return new(confirmado, confirmado is null ? "La confirmación llegó incompleta. Conserva la edición y vuelve a consultar." : null);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return Incierta<T>(); }
        catch (Exception error) when (error is HttpRequestException or JsonException) { return Incierta<T>(); }
    }
    private static ResultadoRemoto<T> Incierta<T>() => new(default,
        "No se confirmó la operación. Conservamos la edición: consulta el estado antes de repetirla.");
}

public sealed class ClienteEscala(IClienteLecturas lecturas, ClienteEvaluacion escritura) : IClienteCatalogo<LetraRemota>
{
    public async Task<ResultadoRemoto<PaginaRemota<LetraRemota>>> ConsultarAsync(ConsultaCatalogo consulta, CancellationToken ct)
    {
        var respuesta = await lecturas.ConsultarAsync<IReadOnlyList<LetraRemota>>($"api/anios-lectivos/{consulta.AnioLectivoId}/escala-cualitativa", ct);
        if (respuesta.Datos is not { } datos || datos.Count > 100 || datos.Any(l => l.AnioLectivoId != consulta.AnioLectivoId || !ValidacionPlanillaRemota.LetraValida(l.Letra)))
            return new(null, respuesta.Error ?? "La escala del año llegó incompleta.");
        var filtradas = datos.Where(l => (l.Letra + " " + l.Descripcion).Contains(consulta.Texto, StringComparison.CurrentCultureIgnoreCase)).ToArray();
        return new(new(filtradas.Skip((consulta.Pagina - 1) * consulta.Tamanio).Take(consulta.Tamanio).ToArray(), consulta.Pagina, consulta.Tamanio, filtradas.Length), null);
    }
    public async Task<ResultadoRemoto<RegistroConfirmado>> GuardarAsync<T>(Guid id, bool nuevo, T datos, CancellationToken ct)
    {
        if (nuevo) return await escritura.EnviarAsync<RegistroConfirmado>(HttpMethod.Post, "api/escala-cualitativa", datos, ct);
        if (datos is not SolicitudRetirarLetra retirar) throw new ArgumentException("Solicitud de retirada no válida.");
        var respuesta = await escritura.EnviarAsync<RegistroConfirmado>(HttpMethod.Delete, $"api/escala-cualitativa/{id}?versionEsperada={retirar.VersionEsperada}", null, ct);
        return respuesta.Error is null ? new(new(id, retirar.VersionEsperada), null) : respuesta;
    }
}
public sealed record SolicitudRetirarLetra(long VersionEsperada);

public sealed class ClientePlanes(IClienteLecturas lecturas, ClienteEvaluacion escritura, Func<Guid, string> nombreMateria) : IClienteCatalogo<PlanMateriaRemota>
{
    public async Task<ResultadoRemoto<PaginaRemota<PlanMateriaRemota>>> ConsultarAsync(ConsultaCatalogo consulta, CancellationToken ct)
    {
        var respuesta = await lecturas.ConsultarAsync<IReadOnlyList<PlanMateriaRemota>>($"api/anios-lectivos/{consulta.AnioLectivoId}/plan-materias", ct);
        if (respuesta.Datos is not { } datos || datos.Count > 500 || datos.Any(p => p.AnioLectivoId != consulta.AnioLectivoId || p.Modalidad is < 1 or > 4))
            return new(null, respuesta.Error ?? "El plan del año no llegó completo.");
        var filtradas = datos.Select(p => p with { Materia = nombreMateria(p.MateriaId) })
            .Where(p => (p.Grado + " " + p.Materia).Contains(consulta.Texto, StringComparison.CurrentCultureIgnoreCase)).OrderBy(p => p.Grado).ThenBy(p => p.Materia).ToArray();
        return new(new(filtradas.Skip((consulta.Pagina - 1) * consulta.Tamanio).Take(consulta.Tamanio).ToArray(), consulta.Pagina, consulta.Tamanio, filtradas.Length), null);
    }
    public Task<ResultadoRemoto<RegistroConfirmado>> GuardarAsync<T>(Guid id, bool nuevo, T datos, CancellationToken ct) =>
        escritura.EnviarAsync<RegistroConfirmado>(HttpMethod.Put, "api/plan-materias/" + id, datos, ct);
}
