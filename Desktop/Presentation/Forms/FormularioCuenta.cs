using Clarisa.Desktop.Services;
using Clarisa.Desktop.Services.Catalogos;

namespace Clarisa.Desktop.Presentation.Forms;
public sealed record PerfilCuenta(string Valor, string Nombre);
public sealed class FormularioCuenta : FormularioRegistro
{
    private string usuario;
    private string nombre;
    private PerfilCuenta? perfil;
    private OpcionRemota? docente;
    private bool activo;
    private bool claveEditada;
    public FormularioCuenta(IReadOnlyList<OpcionRemota> docentes, CuentaRemota? cuenta = null)
    {
        if (cuenta?.DocenteId is { } vinculado && docentes.All(d => d.Id != vinculado))
            docentes = docentes.Append(new OpcionRemota(vinculado, "Docente vinculado a esta cuenta")).ToArray();
        Docentes = docentes;
        Id = cuenta?.Id ?? Guid.CreateVersion7();
        Version = cuenta?.Version ?? 0;
        usuario = cuenta?.Usuario ?? "";
        nombre = cuenta?.Nombre ?? "";
        perfil = Perfiles.FirstOrDefault(p => p.Valor == cuenta?.Perfil);
        docente = docentes.FirstOrDefault(d => d.Id == cuenta?.DocenteId);
        activo = cuenta?.Activo ?? true;
        Inicializar();
    }

    public IReadOnlyList<PerfilCuenta> Perfiles { get; } = [new("Administracion", "Dirección"), new("Docente", "Docente")];
    public IReadOnlyList<OpcionRemota> Docentes { get; }
    public string Usuario { get => usuario; set => Cambiar(ref usuario, value ?? ""); }
    public string Nombre { get => nombre; set => Cambiar(ref nombre, value ?? ""); }

    public PerfilCuenta? Perfil
    {
        get => perfil;
        set
        {
            Cambiar(ref perfil, value);
            Notificar(nameof(EsDocente));
        }
    }

    public OpcionRemota? Docente { get => docente; set => Cambiar(ref docente, value); }
    public bool Activo { get => activo; set => Cambiar(ref activo, value); }
    public bool ClaveEditada { get => claveEditada; set => Cambiar(ref claveEditada, value); }
    public bool EsDocente => perfil?.Valor == "Docente";
    protected override object EstadoActual => (usuario, nombre, perfil?.Valor, docente?.Id, activo, claveEditada);
    protected override IReadOnlyList<string> Campos => [nameof(Usuario), nameof(Nombre), nameof(Perfil), nameof(Docente)];

    protected override string ErrorCampo(string campo) => campo switch
    {
        nameof(Usuario) => usuario.Trim().Length is < 3 or > 40 || !usuario.Trim().All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-')
            ? "Usa entre 3 y 40 letras, números, puntos, guiones o guiones bajos." : "",
        nameof(Nombre) => Requerido(nombre, "el nombre de la cuenta", 80),
        nameof(Perfil) => perfil is null ? "Selecciona el perfil de acceso." : "",
        nameof(Docente) => EsDocente && docente is null ? "Selecciona el docente de esta cuenta." : "",
        _ => ""
    };
    public override object CrearSolicitud() => new CambioCuenta(Id, Version, Usuario.Trim(), Nombre.Trim(), perfil?.Valor ?? "", EsDocente ? docente?.Id : null, Activo);
}
