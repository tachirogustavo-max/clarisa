namespace Clarisa.Application.Common.Resultados;

public sealed record RegistroVersionado(Guid Id, long Version);

public sealed record EliminarRegistro(Guid Id, long VersionEsperada);
