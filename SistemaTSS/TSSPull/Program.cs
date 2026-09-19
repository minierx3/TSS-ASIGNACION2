using System;
using System.IO;
using System.Threading;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

// ── Carga de configuración ──────────────────────────────────────────────────
IConfiguration config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .Build();

string connectionString = config.GetConnectionString("TSSDB")
    ?? throw new InvalidOperationException("Connection string 'TSSDB' no encontrada en appsettings.json.");

string inboundPath   = config["Paths:Inbound"]    ?? @"C:\TSS_Integracion\Inbound";
string procesadosPath = config["Paths:Procesados"] ?? @"C:\TSS_Integracion\Procesados";

// Garantiza que las carpetas existan
Directory.CreateDirectory(inboundPath);
Directory.CreateDirectory(procesadosPath);

Console.WriteLine("=== TSSPull — Servidor Receptor TSS ===");
Console.WriteLine($"Escuchando: {inboundPath}");
Console.WriteLine("Esperando archivos *.txt ... (Ctrl+C para salir)");
Console.WriteLine();

// ── 1. Monitoreo de Directorio (FileSystemWatcher) ──────────────────────────
using FileSystemWatcher watcher = new FileSystemWatcher(inboundPath, "*.txt");

watcher.Created            += OnArchivoCreado;
watcher.EnableRaisingEvents = true;

// Mantiene el programa en ejecución indefinidamente
Thread.Sleep(Timeout.Infinite);


// ── Manejador del evento Created ────────────────────────────────────────────
void OnArchivoCreado(object sender, FileSystemEventArgs e)
{
    Console.WriteLine($"[DETECTADO]  Archivo detectado: {e.Name}");

    // Pausa de 1 segundo para asegurar que el archivo terminó de escribirse
    Thread.Sleep(1000);

    try
    {
        ProcesarArchivo(e.FullPath);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERROR]      {ex.Message}");
    }
}


// ── Lógica principal de procesamiento ───────────────────────────────────────
void ProcesarArchivo(string rutaArchivo)
{
    string nombreArchivo = Path.GetFileName(rutaArchivo);
    decimal totalSalarios = 0;
    int registrosValidos  = 0;

    Console.WriteLine($"[VALIDANDO]  Leyendo y validando registros de: {nombreArchivo}");

    // ── 2. Lectura, Validación e Inserción (ADO.NET) ────────────────────────
    using (SqlConnection connection = new SqlConnection(connectionString))
    {
        connection.Open();

        using StreamReader reader = new StreamReader(rutaArchivo);
        string? linea;

        while ((linea = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(linea)) continue;

            string[] campos = linea.Split(',');

            // Estructura esperada: Cedula, Nombres, Apellidos, SalarioBase, TipoNovedad
            if (campos.Length < 5)
            {
                Console.WriteLine($"[OMITIDO]    Línea con formato incorrecto: {linea}");
                continue;
            }

            string  cedula      = campos[0].Trim();
            string  nombres     = campos[1].Trim();
            string  apellidos   = campos[2].Trim();
            string  tipoNovedad = campos[4].Trim();
            bool    salarioOk   = decimal.TryParse(
                campos[3].Trim(),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out decimal salarioBase);

            // Validaciones de negocio
            if (cedula.Length != 11)
            {
                Console.WriteLine($"[OMITIDO]    Cédula inválida (debe tener 11 caracteres): {cedula}");
                continue;
            }

            if (!salarioOk || salarioBase <= 0)
            {
                Console.WriteLine($"[OMITIDO]    Salario inválido para cédula {cedula}: {campos[3]}");
                continue;
            }

            // Inserción en Recepcion_Autodeterminacion
            string insertSql = @"
                INSERT INTO Recepcion_Autodeterminacion
                    (Cedula, Nombres, Apellidos, SalarioBase, TipoNovedad)
                VALUES
                    (@Cedula, @Nombres, @Apellidos, @SalarioBase, @TipoNovedad)";

            using SqlCommand cmd = new SqlCommand(insertSql, connection);
            cmd.Parameters.AddWithValue("@Cedula",      cedula);
            cmd.Parameters.AddWithValue("@Nombres",     nombres);
            cmd.Parameters.AddWithValue("@Apellidos",   apellidos);
            cmd.Parameters.AddWithValue("@SalarioBase", salarioBase);
            cmd.Parameters.AddWithValue("@TipoNovedad", tipoNovedad);
            cmd.ExecuteNonQuery();

            totalSalarios += salarioBase;
            registrosValidos++;
            Console.WriteLine($"[GUARDANDO]  Cédula {cedula} — RD$ {salarioBase:N2} insertado.");
        }
    } // Cierra SqlConnection y StreamReader

    Console.WriteLine($"[GUARDANDO]  {registrosValidos} registro(s) insertados en Recepcion_Autodeterminacion.");

    // ── 3. Motor de Cálculo y Facturación ───────────────────────────────────
    if (registrosValidos > 0)
    {
        decimal cobroSFS       = Math.Round(totalSalarios * 0.100m, 2); // 10%
        decimal cobroPensiones = Math.Round(totalSalarios * 0.090m, 2); // 9%
        decimal cobroRiesgos   = Math.Round(totalSalarios * 0.012m, 2); // 1.2%
        decimal totalPagar     = cobroSFS + cobroPensiones + cobroRiesgos;

        using SqlConnection connection = new SqlConnection(connectionString);
        connection.Open();

        string facturaSql = @"
            INSERT INTO Facturacion_SDSS
                (Fecha, TotalSalarios, CobroSFS, CobroPensiones, CobroRiesgos, TotalPagar)
            VALUES
                (@Fecha, @TotalSalarios, @CobroSFS, @CobroPensiones, @CobroRiesgos, @TotalPagar)";

        using SqlCommand cmdFactura = new SqlCommand(facturaSql, connection);
        cmdFactura.Parameters.AddWithValue("@Fecha",          DateTime.Now);
        cmdFactura.Parameters.AddWithValue("@TotalSalarios",  totalSalarios);
        cmdFactura.Parameters.AddWithValue("@CobroSFS",       cobroSFS);
        cmdFactura.Parameters.AddWithValue("@CobroPensiones", cobroPensiones);
        cmdFactura.Parameters.AddWithValue("@CobroRiesgos",   cobroRiesgos);
        cmdFactura.Parameters.AddWithValue("@TotalPagar",     totalPagar);
        cmdFactura.ExecuteNonQuery();

        Console.WriteLine();
        Console.WriteLine("[FACTURA]    === Resumen de Facturación SDSS ===");
        Console.WriteLine($"[FACTURA]    Total Salarios   : RD$ {totalSalarios:N2}");
        Console.WriteLine($"[FACTURA]    CobroSFS  (10%)  : RD$ {cobroSFS:N2}");
        Console.WriteLine($"[FACTURA]    Pensiones  (9%)  : RD$ {cobroPensiones:N2}");
        Console.WriteLine($"[FACTURA]    Riesgos  (1.2%)  : RD$ {cobroRiesgos:N2}");
        Console.WriteLine($"[FACTURA]    TOTAL A PAGAR    : RD$ {totalPagar:N2}");
        Console.WriteLine("[FACTURA]    Factura generada y guardada en Facturacion_SDSS.");
        Console.WriteLine();
    }

    // ── 4. Cierre y Trazabilidad: mover archivo a Procesados ────────────────
    string destino = Path.Combine(procesadosPath, Path.GetFileName(rutaArchivo));

    // Evita colisión si el archivo ya existe en Procesados
    if (File.Exists(destino))
    {
        string sinExt    = Path.GetFileNameWithoutExtension(rutaArchivo);
        string extension = Path.GetExtension(rutaArchivo);
        destino = Path.Combine(procesadosPath,
            $"{sinExt}_{DateTime.Now:yyyyMMdd_HHmmss}{extension}");
    }

    File.Move(rutaArchivo, destino);
    Console.WriteLine($"[PROCESADO]  Archivo movido a Procesados: {Path.GetFileName(destino)}");
    Console.WriteLine();
}
