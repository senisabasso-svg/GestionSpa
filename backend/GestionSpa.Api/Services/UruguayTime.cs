namespace GestionSpa.Api.Services;

public static class UruguayTime
{
    private static readonly TimeZoneInfo Tz = TimeZoneInfo.FindSystemTimeZoneById(
        OperatingSystem.IsWindows() ? "Montevideo Standard Time" : "America/Montevideo");

    public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Tz);

    public static DateTime Today => Now.Date;

    public static (int Mes, int Anio) MesAnioActual() => (Now.Month, Now.Year);

    /// <summary>Inicio del día calendario en Uruguay, como UTC (para queries timestamptz).</summary>
    public static DateTime InicioDiaUtc(DateTime? fechaLocal = null)
    {
        var local = (fechaLocal ?? Today).Date;
        return TimeZoneInfo.ConvertTimeToUtc(
            new DateTime(local.Year, local.Month, local.Day, 0, 0, 0, DateTimeKind.Unspecified), Tz);
    }

    public static DateTime FinDiaUtc(DateTime? fechaLocal = null) => InicioDiaUtc(fechaLocal).AddDays(1);

    public static DateTime InicioMesUtc(int mes, int anio) =>
        TimeZoneInfo.ConvertTimeToUtc(new DateTime(anio, mes, 1, 0, 0, 0, DateTimeKind.Unspecified), Tz);

    public static DateTime FinMesUtc(int mes, int anio) => InicioMesUtc(mes, anio).AddMonths(1);

    public static DateTime VencimientoCuota(int mes, int anio) =>
        TimeZoneInfo.ConvertTimeToUtc(new DateTime(anio, mes, 10, 23, 59, 59, DateTimeKind.Unspecified), Tz);

    public static bool EsDespuesDelDia10() => Now.Day > 10;
}
