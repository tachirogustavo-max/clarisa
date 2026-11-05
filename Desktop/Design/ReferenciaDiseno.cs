using Clarisa.Desktop.Services.Catalogos;

namespace Clarisa.Desktop.Design;
internal static class ReferenciaDiseno
{
    public static readonly Guid AnioId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    public static readonly Guid SeccionId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    public static readonly Guid MateriaId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    public static readonly Guid DocenteId = Guid.Parse("40000000-0000-0000-0000-000000000001");
    public static readonly Guid EstudianteId = Guid.Parse("50000000-0000-0000-0000-000000000001");
    public static readonly Guid LapsoId = Guid.Parse("60000000-0000-0000-0000-000000000001");
    public static IReadOnlyList<OpcionRemota> Anios { get; } = [new(AnioId, "2026–2027")];
    public static IReadOnlyList<OpcionRemota> Secciones { get; } = [new(SeccionId, "1er año · A", AnioId)];
    public static IReadOnlyList<OpcionRemota> Materias { get; } = [new(MateriaId, "Matemática")];
    public static IReadOnlyList<OpcionRemota> Docentes { get; } = [new(DocenteId, "Pérez, Ana María")];
    public static IReadOnlyList<OpcionRemota> Estudiantes { get; } = [new(EstudianteId, "Mendoza Rojas, Ana María")];
    public static IReadOnlyList<OpcionRemota> Lapsos { get; } = [new(LapsoId, "Primer momento", AnioId)];
}
