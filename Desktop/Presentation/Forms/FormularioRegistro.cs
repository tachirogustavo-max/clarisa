using System.ComponentModel;
using System.Runtime.CompilerServices;
using Clarisa.Desktop.Presentation.Models;

namespace Clarisa.Desktop.Presentation.Forms;
public abstract class FormularioRegistro : ModeloObservable, IDataErrorInfo
{
    private readonly HashSet<string> tocados = [];
    private readonly Dictionary<string, string> erroresServidor = [];
    private bool mostrarTodos;
    private object? original;
    public Guid Id { get; protected set; }
    public long Version { get; protected set; }
    public bool Nuevo => Version == 0;
    public bool HayCambios => !Equals(original, EstadoActual);
    public long RevisionLocal { get; private set; }
    protected abstract object EstadoActual { get; }
    protected abstract IReadOnlyList<string> Campos { get; }

    protected abstract string ErrorCampo(string campo);
    public bool HayErrores => Campos.Any(c => ErrorActual(c).Length > 0);
    public string? PrimerCampoInvalido => Campos.FirstOrDefault(c => ErrorActual(c).Length > 0);
    public string Error => "";

    public string this[string campo] => mostrarTodos || tocados.Contains(campo) ? ErrorActual(campo) : "";
    private string ErrorActual(string campo) => erroresServidor.GetValueOrDefault(campo) ?? ErrorCampo(campo);
    public void AplicarErrorServidor(string? campoServidor, string mensaje)
    {
        var campo = campoServidor switch
        {
            "Identificacion" => "Cedula",
            "DocenteId" => "Docente",
            "EstudianteId" => "Estudiante",
            "SeccionId" => "Seccion",
            "MateriaId" => "Materia",
            "AnioLectivoId" => "Anio",
            _ => campoServidor
        };
        if (campo is null || !Campos.Contains(campo)) return;
        erroresServidor[campo] = mensaje;
        tocados.Add(campo);
        Notificar(campo);
        Notificar("Item[]");
        Notificar(nameof(HayErrores));
    }
    public bool Validar()
    {
        var anteriores = erroresServidor.Keys.ToArray();
        erroresServidor.Clear();
        mostrarTodos = true;
        foreach (var campo in anteriores) Notificar(campo);
        Notificar("Item[]");
        return !HayErrores;
    }

    protected void Cambiar<T>(ref T campo, T valor, [CallerMemberName] string nombre = "")
    {
        if (EqualityComparer<T>.Default.Equals(campo, valor)) return;
        erroresServidor.Remove(nombre);
        Establecer(ref campo, valor, nombre);
        RevisionLocal++;
        tocados.Add(nombre);
        Notificar("Item[]");
        Notificar(nameof(HayCambios));
        Notificar(nameof(HayErrores));
    }

    protected static string Requerido(string? valor, string nombre, int maximo) => string.IsNullOrWhiteSpace(valor) ? "Escribe " + nombre + "." : valor.Trim().Length > maximo ? "Admite hasta " + maximo + " caracteres." : "";
    public abstract object CrearSolicitud();
    protected void Inicializar() => original = EstadoActual;
    public Action<long> PrepararConfirmacion()
    {
        var enviada = EstadoActual;
        return version =>
        {
            original = enviada;
            Version = version;
            Notificar(nameof(Nuevo));
            Notificar(nameof(HayCambios));
        };
    }
}
