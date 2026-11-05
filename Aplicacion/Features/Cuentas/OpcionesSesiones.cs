namespace Clarisa.Application.Features.Cuentas;

public sealed record OpcionesSesiones
{
    public int HorasSesion { get; init; } = 9;
    public int MaximoSesionesPorCuenta { get; init; } = 5;
    public int DiasConservarVencidas { get; init; } = 30;

    public TimeSpan Duracion => TimeSpan.FromHours(HorasSesion);

    public OpcionesSesiones Validar()
    {
        if (HorasSesion is < 1 or > 24) throw new ArgumentException("La sesión debe durar de 1 a 24 horas.", nameof(HorasSesion));
        if (MaximoSesionesPorCuenta is < 1 or > 20)
            throw new ArgumentException("Usa de 1 a 20 sesiones por cuenta.", nameof(MaximoSesionesPorCuenta));
        if (DiasConservarVencidas is < 1 or > 365)
            throw new ArgumentException("Usa de 1 a 365 días.", nameof(DiasConservarVencidas));
        return this;
    }
}

public sealed record OpcionesLimiteIntentos
{
    public int FallosPorUsuario { get; init; } = 5;
    public int FallosPorOrigen { get; init; } = 30;
    public TimeSpan Ventana { get; init; } = TimeSpan.FromMinutes(15);
    public TimeSpan Bloqueo { get; init; } = TimeSpan.FromMinutes(15);
    public int MaximoEntradas { get; init; } = 10_000;
    // El valor según los núcleos del servidor lo decide la composición del host; el núcleo no consulta el entorno.
    public int VerificacionesSimultaneas { get; init; } = 2;
    public TimeSpan EsperaVerificacion { get; init; } = TimeSpan.FromSeconds(10);

    public OpcionesLimiteIntentos Validar()
    {
        if (VerificacionesSimultaneas is < 1 or > 64) throw new ArgumentException("Usa de 1 a 64 verificaciones simultáneas.");
        if (EsperaVerificacion <= TimeSpan.Zero || EsperaVerificacion > TimeSpan.FromMinutes(1))
            throw new ArgumentException("La espera de verificación debe durar entre un instante y un minuto.");
        if (FallosPorUsuario is < 1 or > 100 || FallosPorOrigen is < 1 or > 1000)
            throw new ArgumentException("Los umbrales de intentos no son válidos.");
        if (Ventana <= TimeSpan.Zero || Bloqueo <= TimeSpan.Zero || Ventana > TimeSpan.FromDays(1) || Bloqueo > TimeSpan.FromDays(1))
            throw new ArgumentException("La ventana y el bloqueo deben durar entre un instante y un día.");
        if (MaximoEntradas is < 2 or > 1_000_000) throw new ArgumentException("El máximo de entradas no es válido.");
        return this;
    }
}
