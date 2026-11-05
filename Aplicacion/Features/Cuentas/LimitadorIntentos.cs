using Clarisa.Application.Abstractions;

namespace Clarisa.Application.Features.Cuentas;

// Cuenta fallos por usuario escrito (exista o no, para no revelar cuentas) y por origen. Vive en memoria del servidor: se reinicia
// con él. El número de entradas está acotado para que un atacante no pueda agotar la memoria inventando usuarios.
// Cada intento se reserva antes de verificar la clave y cuenta contra el umbral mientras está en curso: varios intentos simultáneos
// no superan el límite que tendrían uno tras otro. Tras reservar, el llamador debe terminar con un fallo, un acierto o una liberación.
public sealed class LimitadorIntentos
{
    // Respuesta cuando el umbral solo está ocupado por intentos que todavía se están verificando.
    public static readonly TimeSpan EsperaIntentosEnCurso = TimeSpan.FromSeconds(2);

    private sealed class Contador
    {
        public int Fallos;
        public int EnCurso;
        public DateTimeOffset InicioVentana;
        public DateTimeOffset? BloqueadoHasta;
        public DateTimeOffset Ultimo;
    }

    private readonly IReloj reloj;
    private readonly OpcionesLimiteIntentos opciones;
    private readonly Dictionary<string, Contador> contadores = new(StringComparer.Ordinal);
    private readonly Lock cerrojo = new();

    public LimitadorIntentos(IReloj reloj, OpcionesLimiteIntentos opciones)
    {
        ArgumentNullException.ThrowIfNull(reloj);
        this.reloj = reloj;
        this.opciones = opciones.Validar();
    }

    public int Entradas
    {
        get
        {
            lock (cerrojo) return contadores.Count;
        }
    }

    // Null si el intento quedó reservado; si no, el tiempo que debe esperar.
    public TimeSpan? Reservar(string usuario, string origen)
    {
        var ahora = reloj.Ahora;
        lock (cerrojo)
        {
            var porUsuario = Obtener(ClaveUsuario(usuario), ahora);
            var porOrigen = Obtener(ClaveOrigen(origen), ahora);
            var espera = new[] { Espera(porUsuario, opciones.FallosPorUsuario, ahora), Espera(porOrigen, opciones.FallosPorOrigen, ahora) }.Max();
            if (espera > TimeSpan.Zero) return espera;
            porUsuario.EnCurso++;
            porOrigen.EnCurso++;
            return null;
        }
    }

    public void RegistrarFallo(string usuario, string origen)
    {
        var ahora = reloj.Ahora;
        lock (cerrojo)
        {
            Fallar(ClaveUsuario(usuario), opciones.FallosPorUsuario, ahora);
            Fallar(ClaveOrigen(origen), opciones.FallosPorOrigen, ahora);
        }
    }

    // Un acierto limpia los fallos del usuario: el origen conserva los que haya hecho con otros nombres.
    public void RegistrarExito(string usuario, string origen)
    {
        lock (cerrojo)
        {
            if (contadores.TryGetValue(ClaveUsuario(usuario), out var porUsuario))
            {
                porUsuario.EnCurso = Math.Max(0, porUsuario.EnCurso - 1);
                porUsuario.Fallos = 0;
                porUsuario.BloqueadoHasta = null;
                if (porUsuario.EnCurso == 0) contadores.Remove(ClaveUsuario(usuario));
            }
            Terminar(ClaveOrigen(origen));
        }
    }

    // El intento terminó sin decidir (almacén caído, cancelación, sin turno de verificación): ni fallo ni acierto.
    public void Liberar(string usuario, string origen)
    {
        lock (cerrojo)
        {
            Terminar(ClaveUsuario(usuario));
            Terminar(ClaveOrigen(origen));
        }
    }

    private Contador Obtener(string clave, DateTimeOffset ahora)
    {
        if (!contadores.TryGetValue(clave, out var contador))
        {
            HacerEspacio(ahora);
            contador = new Contador { InicioVentana = ahora };
            contadores[clave] = contador;
        }
        contador.Ultimo = ahora;
        if (contador.BloqueadoHasta is not { } hasta || hasta <= ahora)
        {
            contador.BloqueadoHasta = null;
            if (ahora - contador.InicioVentana > opciones.Ventana)
            {
                contador.Fallos = 0;
                contador.InicioVentana = ahora;
            }
        }
        return contador;
    }

    private static TimeSpan Espera(Contador contador, int umbral, DateTimeOffset ahora) =>
        contador.BloqueadoHasta is { } hasta && hasta > ahora ? hasta - ahora
            : contador.Fallos + contador.EnCurso >= umbral ? EsperaIntentosEnCurso
            : TimeSpan.Zero;

    private void Fallar(string clave, int umbral, DateTimeOffset ahora)
    {
        var contador = Obtener(clave, ahora);
        contador.EnCurso = Math.Max(0, contador.EnCurso - 1);
        if (contador.BloqueadoHasta is not null) return;
        contador.Fallos++;
        if (contador.Fallos < umbral) return;
        contador.BloqueadoHasta = ahora + opciones.Bloqueo;
        contador.Fallos = 0;
        contador.InicioVentana = ahora;
    }

    private void Terminar(string clave)
    {
        if (!contadores.TryGetValue(clave, out var contador)) return;
        contador.EnCurso = Math.Max(0, contador.EnCurso - 1);
        if (contador is { EnCurso: 0, Fallos: 0, BloqueadoHasta: null }) contadores.Remove(clave);
    }

    private void HacerEspacio(DateTimeOffset ahora)
    {
        if (contadores.Count < opciones.MaximoEntradas) return;
        foreach (var (clave, contador) in contadores.ToList())
            if (contador.EnCurso == 0 && (contador.BloqueadoHasta is not { } hasta || hasta <= ahora)
                && ahora - contador.InicioVentana > opciones.Ventana)
                contadores.Remove(clave);
        if (contadores.Count < opciones.MaximoEntradas) return;
        // Lleno de entradas vigentes: se descarta la menos reciente, preferentemente sin intentos en curso. Un atacante que llegue
        // aquí ya está limitado por su origen y por el turno global de verificación.
        var candidatas = contadores.Where(par => par.Value.EnCurso == 0).ToList();
        var antigua = (candidatas.Count > 0 ? candidatas : contadores.ToList()).MinBy(par => par.Value.Ultimo).Key;
        contadores.Remove(antigua);
    }

    private static string ClaveUsuario(string usuario) => "u:" + Recortar(usuario).ToLowerInvariant();

    private static string ClaveOrigen(string origen) => "o:" + Recortar(origen);

    private static string Recortar(string? valor) => valor is null ? "" : valor.Length <= 64 ? valor : valor[..64];
}
