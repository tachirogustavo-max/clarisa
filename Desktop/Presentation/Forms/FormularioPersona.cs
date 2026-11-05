using System.Net.Mail;
using Clarisa.Desktop.Services.Catalogos;
using Clarisa.Desktop.Presentation.Formatting;

namespace Clarisa.Desktop.Presentation.Forms;
public sealed class FormularioPersona : FormularioRegistro
{
    private string cedula;
    private string nombres;
    private string apellidos;
    private string correo;
    private string nacimiento;
    private bool activo;
    public FormularioPersona(bool docente, PersonaRemota? persona = null)
    {
        EsDocente = docente;
        Id = persona?.Id ?? Guid.CreateVersion7();
        Version = persona?.Version ?? 0;
        cedula = persona?.Cedula ?? "";
        nombres = persona?.Nombres ?? "";
        apellidos = persona?.Apellidos ?? "";
        correo = persona?.Correo ?? "";
        nacimiento = FechaEscrita.Formatear(persona?.FechaNacimiento?.ToDateTime(TimeOnly.MinValue));
        activo = persona?.Activo ?? true;
        Inicializar();
    }

    public bool EsDocente { get; }
    public string Cedula { get => cedula; set => Cambiar(ref cedula, value ?? ""); }
    public string Nombres { get => nombres; set => Cambiar(ref nombres, value ?? ""); }
    public string Apellidos { get => apellidos; set => Cambiar(ref apellidos, value ?? ""); }
    public string Correo { get => correo; set => Cambiar(ref correo, value ?? ""); }
    public string TextoFechaNacimiento
    {
        get => nacimiento;
        set
        {
            Cambiar(ref nacimiento, value ?? "", nameof(FechaNacimiento));
            Notificar();
        }
    }
    public DateTime? FechaNacimiento
    {
        get => FechaEscrita.Intentar(nacimiento, out var fecha) ? fecha : null;
        set => TextoFechaNacimiento = FechaEscrita.Formatear(value);
    }
    public bool Activo { get => activo; set => Cambiar(ref activo, value); }
    protected override IReadOnlyList<string> Campos => [nameof(Cedula), nameof(Nombres), nameof(Apellidos), nameof(Correo), nameof(FechaNacimiento)];
    protected override object EstadoActual => new EstadoPersona(cedula, nombres, apellidos, correo, nacimiento, activo);

    protected override string ErrorCampo(string campo) => campo switch
    {
        nameof(Nombres) => Requerido(nombres, "los nombres", 80),
        nameof(Apellidos) => Requerido(apellidos, "los apellidos", 80),
        nameof(Cedula) => ValidarCedula(),
        nameof(Correo) => EsDocente && correo.Trim().Length > 0 && (!MailAddress.TryCreate(correo.Trim(), out var direccion) || direccion.Address != correo.Trim()) ? "Revisa el correo electrónico." : "",
        nameof(FechaNacimiento) => !EsDocente && !FechaEscrita.Intentar(nacimiento, out _) ? "Escribe una fecha válida en formato dd/MM/aaaa o usa el calendario." : "",
        _ => ""
    };
    private string ValidarCedula()
    {
        var limpia = cedula.Trim().Replace(" ", "").Replace(".", "");
        if (limpia.Length == 0)
            return EsDocente ? "Escribe la cédula del docente." : "";
        return limpia.Length is < 3 or > 20 || !limpia.All(c => char.IsAsciiLetterOrDigit(c) || c == '-') || limpia.StartsWith('-') || limpia.EndsWith('-') || limpia.Contains("--") ? "Revisa la cédula; puedes escribirla con o sin prefijo." : "";
    }

    public override object CrearSolicitud()
    {
        var identificacion = string.IsNullOrWhiteSpace(cedula) ? null : cedula.Trim();
        if (EsDocente)
            return Nuevo ? new
            {
                Id,
                Identificacion = identificacion,
                Nombres,
                Apellidos,
                Correo = correo.Trim()
            }

            : new
            {
                Id,
                VersionEsperada = Version,
                Identificacion = identificacion,
                Nombres,
                Apellidos,
                Correo = correo.Trim(),
                Activo
            };
        if (!FechaEscrita.Intentar(nacimiento, out var nacimientoValido))
            throw new InvalidOperationException("La fecha escrita debe corregirse antes de crear la solicitud.");
        var fecha = nacimientoValido is { } valor ? DateOnly.FromDateTime(valor) : (DateOnly? )null;
        return Nuevo ? new
        {
            Id,
            Identificacion = identificacion,
            Nombres,
            Apellidos,
            FechaNacimiento = fecha
        }

        : new
        {
            Id,
            VersionEsperada = Version,
            Identificacion = identificacion,
            Nombres,
            Apellidos,
            FechaNacimiento = fecha,
            Activo
        };
    }

    private sealed record EstadoPersona(string Cedula, string Nombres, string Apellidos, string Correo, string Nacimiento, bool Activo);
}
