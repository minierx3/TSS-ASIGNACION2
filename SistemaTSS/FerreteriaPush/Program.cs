using System;
using System.IO;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

// ── Carga de configuración ──────────────────────────────────────────────────
IConfiguration config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .Build();

string connectionString = config.GetConnectionString("FerreteriaDB")
    ?? throw new InvalidOperationException("Connection string 'FerreteriaDB' no encontrada en appsettings.json.");

string inboundPath  = config["Paths:Inbound"] ?? @"C:\TSS_Integracion\Inbound";
string rutaArchivo  = Path.Combine(inboundPath, "Autodeterminacion_Nomina.txt");

// ── Lógica principal con manejo de errores global ───────────────────────────
try
{
    Console.WriteLine("Iniciando extracción...");

    // ── 1. Acceso a Datos: ADO.NET puro ────────────────────────────────────
    using SqlConnection connection = new SqlConnection(connectionString);
    connection.Open();

    SqlCommand command = new SqlCommand(
        "SELECT Cedula, Nombres, Apellidos, SalarioBase FROM Empleados", connection);

    SqlDataReader reader = command.ExecuteReader();

    Console.WriteLine("Generando archivo...");

    // ── 2. Generación del archivo (Push) ───────────────────────────────────
    Directory.CreateDirectory(inboundPath);

    // Elimina el archivo si ya existe para garantizar que FileSystemWatcher
    // dispare el evento Created (y no Changed) en TSSPull.
    if (File.Exists(rutaArchivo))
        File.Delete(rutaArchivo);

    using StreamWriter writer = new StreamWriter(rutaArchivo, append: false);

    while (reader.Read())
    {
        string cedula      = reader["Cedula"].ToString()!;
        string nombres     = reader["Nombres"].ToString()!;
        string apellidos   = reader["Apellidos"].ToString()!;
        string salarioBase = reader["SalarioBase"].ToString()!;

        // Formato CSV con TipoNovedad "REGULAR" al final de cada línea
        string linea = $"{cedula},{nombres},{apellidos},{salarioBase},REGULAR";
        writer.WriteLine(linea);
    }

    reader.Close();

    // ── 3. Confirmación ────────────────────────────────────────────────────
    Console.WriteLine("Archivo enviado a la TSS exitosamente.");
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}
