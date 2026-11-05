using System.Text.Json.Serialization;

namespace Clarisa.Desktop.Services.Catalogos;
public sealed record PaginaRemota<T>(IReadOnlyList<T> Elementos, int Numero, int Tamanio, int Total);
public sealed record ResultadoRemoto<T>(T? Datos, string? Error, string? Campo = null);
public sealed record RegistroConfirmado(Guid Id, long Version);
public sealed record ConsultaCatalogo(int Pagina, int Tamanio, string Texto, bool IncluirInactivos, Guid? AnioLectivoId = null);
public sealed record PersonaRemota(Guid Id, [property: JsonPropertyName("identificacion")] string? Cedula, string Nombres, string Apellidos, DateOnly? FechaNacimiento, string? Correo, bool Activo, long Version)
{
    public string NombreCompleto => Apellidos + ", " + Nombres;
    public string Estado => Activo ? "Activo" : "Inactivo";
}

public sealed record MateriaRemota(Guid Id, string Codigo, string Nombre, bool Activa, long Version);
public sealed record GradoRemoto(Guid Id, string Nombre, int Orden, bool Activo, long Version);
public sealed record SeccionRemota(Guid Id, Guid AnioLectivoId, string Grado, string Nombre, long Version);
public sealed record AnioRemoto(Guid Id, string Nombre, DateOnly Inicio, DateOnly Fin, Guid? LapsoActualId, long Version);
public sealed record OpcionRemota(Guid Id, string Nombre, Guid? AnioLectivoId = null);
