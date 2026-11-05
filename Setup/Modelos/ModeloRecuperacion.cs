using System.Collections.ObjectModel;
using Clarisa.Instalacion.Contratos;

namespace Clarisa.Setup.Modelos;

// Estado de la ventana «Recuperar una copia». Primero se comprueba la copia sin cambiar nada; solo después, con la contraseña de
// mantenimiento y la confirmación, se recupera. Las acciones reales llegan como funciones para poder probar la ventana sin servidor.
public sealed class ModeloRecuperacion : ModeloObservable
{
    private readonly Func<SolicitudRecuperacion, CancellationToken, Task<RevisionRecuperacion>> revisar;
    private readonly Func<SolicitudRecuperacion, CredencialOperador, IProgress<string>, CancellationToken, Task<ResultadoRecuperacion>> recuperar;
    private readonly Func<CredencialOperador, CancellationToken, Task<ResultadoRecuperacion>> volver;
    private string copia = "";
    private string clave = "";
    private bool entendido;
    private bool ocupado;
    private RevisionRecuperacion? revision;
    private string mensaje = "Elige la copia que quieres recuperar y pulsa Comprobar copia. Comprobarla no cambia nada.";

    public ModeloRecuperacion(string? carpetaCopias,
        Func<SolicitudRecuperacion, CancellationToken, Task<RevisionRecuperacion>> revisar,
        Func<SolicitudRecuperacion, CredencialOperador, IProgress<string>, CancellationToken, Task<ResultadoRecuperacion>> recuperar,
        Func<CredencialOperador, CancellationToken, Task<ResultadoRecuperacion>> volver)
    {
        CarpetaCopias = carpetaCopias;
        this.revisar = revisar;
        this.recuperar = recuperar;
        this.volver = volver;
        if (carpetaCopias is null) mensaje = "Este equipo no tiene instalado el servidor. La recuperación se hace en el equipo servidor.";
    }

    public string? CarpetaCopias { get; }
    public bool HayServidor => CarpetaCopias is not null;
    public ObservableCollection<string> Resumen { get; } = [];
    public ObservableCollection<string> Pasos { get; } = [];
    public string Mensaje => mensaje;
    public bool Ocupado => ocupado;
    public bool CopiaComprobada => revision?.Valida == true;
    public bool PuedeComprobar => HayServidor && !ocupado && copia.Length > 0;
    public bool PuedeRecuperar => CopiaComprobada && entendido && !ocupado;
    public bool PuedeVolver => HayServidor && !ocupado;

    public string Copia
    {
        get => copia;
        set { if (copia == value) return; copia = value ?? ""; Olvidar(); }
    }

    public string ClaveRecuperacion
    {
        get => clave;
        set { if (clave == value) return; clave = value ?? ""; Olvidar(); }
    }

    public bool Entendido
    {
        get => entendido;
        set { Cambiar(ref entendido, value); Notificar(nameof(PuedeRecuperar)); }
    }

    public async Task ComprobarAsync()
    {
        if (!PuedeComprobar) return;
        await OcuparAsync("Comprobando la copia. Puede tardar unos minutos con copias grandes.", async () =>
        {
            revision = await revisar(Solicitud(), CancellationToken.None);
            Resumen.Clear();
            foreach (var linea in revision.Resumen) Resumen.Add(linea);
            return revision.Valida
                ? "La copia está completa. Escribe la contraseña de mantenimiento, marca la confirmación y pulsa Recuperar."
                : revision.Mensaje;
        });
    }

    public async Task RecuperarAsync(string contrasena)
    {
        if (!PuedeRecuperar) return;
        if (string.IsNullOrEmpty(contrasena))
        {
            Informar("Escribe la contraseña de mantenimiento que se eligió al instalar el servidor.");
            return;
        }
        await OcuparAsync("Recuperando la copia. No apagues el equipo.", async () =>
        {
            Pasos.Clear();
            using var credencial = new CredencialOperador(CuentaMantenimiento.Usuario, contrasena);
            var resultado = await recuperar(Solicitud(), credencial, new Progress<string>(Pasos.Add), CancellationToken.None);
            return resultado.Mensaje;
        });
    }

    public async Task VolverAsync(string contrasena)
    {
        if (!PuedeVolver) return;
        if (string.IsNullOrEmpty(contrasena))
        {
            Informar("Escribe la contraseña de mantenimiento para volver a la información anterior.");
            return;
        }
        await OcuparAsync("Volviendo a la información anterior.", async () =>
        {
            using var credencial = new CredencialOperador(CuentaMantenimiento.Usuario, contrasena);
            return (await volver(credencial, CancellationToken.None)).Mensaje;
        });
    }

    private SolicitudRecuperacion Solicitud() => new(copia, clave.Length == 0 ? null : clave);

    private async Task OcuparAsync(string inicio, Func<Task<string>> accion)
    {
        ocupado = true;
        Informar(inicio);
        try { Informar(await accion()); }
        finally
        {
            ocupado = false;
            Estado();
        }
    }

    private void Olvidar()
    {
        revision = null;
        Resumen.Clear();
        Notificar(nameof(Copia));
        Notificar(nameof(ClaveRecuperacion));
        Estado();
    }

    private void Informar(string texto)
    {
        mensaje = texto;
        Notificar(nameof(Mensaje));
        Estado();
    }

    private void Estado()
    {
        foreach (var propiedad in new[] { nameof(Ocupado), nameof(CopiaComprobada), nameof(PuedeComprobar), nameof(PuedeRecuperar), nameof(PuedeVolver) })
            Notificar(propiedad);
    }
}
