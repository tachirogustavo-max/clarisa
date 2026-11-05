using Clarisa.Application.Abstractions;

namespace Clarisa.Application.Features.Respaldos;

// Hora del día (en la hora local del servidor) a partir de la cual se pide la copia diaria y cuántas copias automáticas se conservan.
public sealed record ReglasCopiasAutomaticas(bool Activas, TimeOnly Hora, int Conservar)
{
    public const int MinimoConservar = 7;
    public const int MaximoConservar = 365;
}

// Copia diaria que solicita el propio servidor, sin tareas programadas de Windows. Usa el mismo trabajo que «Crear respaldo»: copia
// cifrada, verificada y publicada. Si el servidor estaba apagado a la hora prevista, la copia se pide en cuanto vuelve a funcionar.
// Solo se retiran copias automáticas antiguas; las que pide una persona nunca se borran solas.
public sealed class CopiasAutomaticas(IRegistroOperacionesRespaldo registro, IAlmacenCopias almacen, IClaveRespaldos clave,
    CoordinacionRespaldos coordinacion, IReloj reloj)
{
    // Identifica las operaciones que pidió el servidor y no una cuenta.
    public static readonly Guid Sistema = new("00000000-0000-0000-0000-0000000c0a17");

    // Tras un intento fallido se espera antes de repetirlo, para no llenar el registro con el mismo fallo cada minuto.
    private static readonly TimeSpan EsperaTrasFallo = TimeSpan.FromHours(1);

    public async Task<Guid?> RevisarAsync(ReglasCopiasAutomaticas reglas, TimeZoneInfo zona, CancellationToken ct)
    {
        if (!reglas.Activas || !coordinacion.Lista) return null;
        using var turno = await coordinacion.TomarAceptacionAsync(ct);
        var operaciones = await registro.ListarAsync(ct);
        await RetirarAntiguasAsync(operaciones, reglas.Conservar, ct);

        var ahora = reloj.Ahora;
        var local = TimeZoneInfo.ConvertTime(ahora, zona);
        if (TimeOnly.FromDateTime(local.DateTime) < reglas.Hora) return null;
        if (operaciones.Any(o => o.Tipo == TipoOperacionRespaldo.Respaldo && o.Abierta)) return null;
        var yaHoy = operaciones.Any(o => o.SolicitadaPor == Sistema && TimeZoneInfo.ConvertTime(o.SolicitadaUtc, zona).Date == local.Date
            && (o.Estado != EstadoOperacionRespaldo.Fallida || ahora - o.SolicitadaUtc < EsperaTrasFallo));
        if (yaHoy || !await clave.EstaPreparadaAsync(ct) || !almacen.HayEspacioSuficiente()) return null;

        var nueva = new OperacionRespaldo(Guid.CreateVersion7(ahora), TipoOperacionRespaldo.Respaldo, EstadoOperacionRespaldo.Pendiente,
            FaseOperacionRespaldo.Aceptada, MensajesRespaldo.Automatica, Sistema, ahora, ahora);
        var (operacion, creada) = await registro.CrearSiNoExisteAsync(nueva, CancellationToken.None);
        if (creada) coordinacion.Avisar();
        return operacion.Id;
    }

    private async Task RetirarAntiguasAsync(IReadOnlyList<OperacionRespaldo> operaciones, int conservar, CancellationToken ct)
    {
        var sobrantes = operaciones.Where(o => o.SolicitadaPor == Sistema && o.CopiaPublicada)
            .OrderByDescending(o => o.InstanteCopia).ThenByDescending(o => o.Id).Skip(conservar);
        foreach (var antigua in sobrantes)
        {
            almacen.RetirarPublicada(antigua.Id);
            await registro.GuardarAsync(antigua with
            {
                Estado = EstadoOperacionRespaldo.Retirada, Mensaje = MensajesRespaldo.Retirada, ActualizadaUtc = reloj.Ahora
            }, ct);
        }
    }
}
