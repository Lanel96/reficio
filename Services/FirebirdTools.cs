using System.Diagnostics;
using System.Text;
using Reficio.Models;

namespace Reficio.Services;

public static class FirebirdTools
{
    public static RepairResult RunTool(string binDir, string tool, string dbPath,
        string user, string password, params string[] extraArgs)
        => RunTool(binDir, tool, dbPath, user, password, true, extraArgs);
    
    public static RepairResult RunTool(string binDir, string tool, string dbPath,
        string user, string password, bool appendDbPath, params string[] extraArgs)
    {
        var result = new RepairResult { Step = tool };
        try
        {
            var exeName = OperatingSystem.IsWindows() ? $"{tool}.exe" : tool;
            var binPath = string.IsNullOrEmpty(binDir) ? exeName : Path.Combine(binDir, exeName);
            
            if (!File.Exists(binPath))
            {
                result.Error = $"No se encuentra la herramienta: {binPath}";
                LogService.LogError($"Herramienta no encontrada: {binPath}");
                return result;
            }
            
            var psi = new ProcessStartInfo
            {
                FileName = binPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            psi.ArgumentList.Add("-user");
            psi.ArgumentList.Add(user);
            psi.ArgumentList.Add("-password");
            psi.ArgumentList.Add(password);
            foreach (var arg in extraArgs) psi.ArgumentList.Add(arg);
            if (appendDbPath) psi.ArgumentList.Add(dbPath);
            
            LogService.Log($"Ejecutando: {tool} {string.Join(" ", psi.ArgumentList)}");
            
            using var process = Process.Start(psi);
            if (process == null) { result.Error = $"No se pudo iniciar {tool}"; return result; }
            
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            
            result.Output = output + (string.IsNullOrEmpty(error) ? "" : "\nSTDERR:\n" + error);
            result.ExitCode = process.ExitCode;
            result.Success = process.ExitCode == 0;
            if (!result.Success) result.Error = $"{tool} terminó con código {process.ExitCode}";
            
            LogService.Log($"{tool} completado con código {process.ExitCode}", result.Success ? LogLevel.Info : LogLevel.Warning);
        }
        catch (Exception ex)
        {
            result.Error = $"Error ejecutando {tool}: {ex.Message}";
            LogService.LogError($"Error ejecutando {tool}", ex);
        }
        return result;
    }
    
    public static RepairResult Diagnosticar(string binDir, string dbPath, string user, string password, ProgressCallback? onProgress = null)
    {
        onProgress?.Invoke(0.05, "Leyendo encabezado con gstat...");
        var headerResult = RunTool(binDir, "gstat", dbPath, user, password, "-header");
        
        onProgress?.Invoke(0.3, "Validando integridad con gfix...");
        var validateResult = RunTool(binDir, "gfix", dbPath, user, password, "-validate", "-full", "-no_update");
        
        onProgress?.Invoke(0.8, "Analizando resultados...");
        var output = $"=== ENCABEZADO (gstat) ===\n{headerResult.Output}\n\n=== VALIDACIÓN (gfix) ===\n{validateResult.Output}";
        var hasProblems = !validateResult.Success ||
                          output.ToLower().Contains("damaged") ||
                          output.ToLower().Contains("corrupt") ||
                          output.ToLower().Contains("error");
        
        var result = new RepairResult
        {
            Success = !hasProblems,
            Output = output,
            Step = "Diagnóstico",
            Error = hasProblems ? "Se detectaron problemas en la base de datos" : ""
        };
        
        onProgress?.Invoke(1.0, result.Success ? "Diagnóstico completado — BD saludable" : "Diagnóstico completado — problemas detectados");
        return result;
    }
    
    public static RepairResult RepararLigero(string binDir, string dbPath, string user, string password, ProgressCallback? onProgress = null)
    {
        onProgress?.Invoke(0.1, "Iniciando reparación ligera (gfix -validate -full)...");
        var result = RunTool(binDir, "gfix", dbPath, user, password, "-validate", "-full");
        onProgress?.Invoke(1.0, result.Success ? "Reparación ligera completada" : "Error en reparación ligera");
        return result;
    }
    
    public static RepairResult RepararProfundo(string binDir, string dbPath, string user, string password, string backupPath, string restoredPath, ProgressCallback? onProgress = null)
    {
        var output = new StringBuilder();
        
        onProgress?.Invoke(0.05, "Paso 1/4: Marcando registros dañados (gfix -mend)...");
        var mendResult = RunTool(binDir, "gfix", dbPath, user, password, "-mend", "-ignore");
        output.AppendLine($"=== MEND ===\n{mendResult.Output}");
        if (!mendResult.Success)
            output.AppendLine($"Nota: gfix mend terminó con código {mendResult.ExitCode} (puede ser normal)");
        
        onProgress?.Invoke(0.2, "Paso 2/4: Generando backup (gbak -b)...");
        var backupResult = RunTool(binDir, "gbak", dbPath, user, password, false, "-b", "-g", "-v", dbPath, backupPath);
        output.AppendLine($"\n=== BACKUP ===\n{backupResult.Output}");
        if (!backupResult.Success)
        {
            onProgress?.Invoke(1.0, "Error en backup");
            return new RepairResult { Success = false, Output = output.ToString(), Error = $"Backup falló: {backupResult.Error}", Step = "Reparación Profunda" };
        }
        
        onProgress?.Invoke(0.5, "Paso 3/4: Restaurando base de datos (gbak -c)...");
        var restoreResult = RunTool(binDir, "gbak", backupPath, user, password, false, "-c", "-v", backupPath, restoredPath);
        output.AppendLine($"\n=== RESTORE ===\n{restoreResult.Output}");
        if (!restoreResult.Success)
        {
            onProgress?.Invoke(1.0, "Error en restauración");
            return new RepairResult { Success = false, Output = output.ToString(), Error = $"Restauración falló: {restoreResult.Error}", Step = "Reparación Profunda" };
        }
        
        onProgress?.Invoke(0.85, "Paso 4/4: Limpiando basura (gfix -sweep)...");
        var sweepResult = RunTool(binDir, "gfix", restoredPath, user, password, "-sweep");
        output.AppendLine($"\n=== SWEEP ===\n{sweepResult.Output}");
        
        onProgress?.Invoke(1.0, "Reparación profunda completada");
        return new RepairResult { Success = true, Output = output.ToString(), Step = "Reparación Profunda" };
    }
    
    public static RepairResult SoloBackup(string binDir, string dbPath, string user, string password, string backupPath, ProgressCallback? onProgress = null)
    {
        onProgress?.Invoke(0.1, "Iniciando backup (gbak -b -g -v)...");
        var result = RunTool(binDir, "gbak", dbPath, user, password, false, "-b", "-g", "-v", dbPath, backupPath);
        onProgress?.Invoke(1.0, result.Success ? "Backup completado" : "Error en backup");
        return result;
    }
    
    public static RepairResult VerificarIntegridad(string binDir, string dbPath, string user, string password, ProgressCallback? onProgress = null)
    {
        onProgress?.Invoke(0.1, "Verificando integridad (gfix -validate -full -no_update)...");
        var result = RunTool(binDir, "gfix", dbPath, user, password, "-validate", "-full", "-no_update");
        onProgress?.Invoke(1.0, result.Success ? "Integridad verificada — sin errores" : "Integridad verificada — errores detectados");
        return result;
    }
    
    public static RepairResult Sweep(string binDir, string dbPath, string user, string password, ProgressCallback? onProgress = null)
    {
        onProgress?.Invoke(0.1, "Ejecutando limpieza de transacciones (gfix -sweep)...");
        var result = RunTool(binDir, "gfix", dbPath, user, password, "-sweep");
        onProgress?.Invoke(1.0, result.Success ? "Limpieza completada" : "Error en limpieza");
        return result;
    }
    
    public static RepairResult NBackup(string binDir, string dbPath, string user, string password, string backupPath, int level = 0, ProgressCallback? onProgress = null)
    {
        onProgress?.Invoke(0.1, $"Iniciando nbackup nivel {level}...");
        var result = RunTool(binDir, "nbackup", dbPath, user, password, false, "-B", level > 0 ? level.ToString() : "0", dbPath, backupPath);
        onProgress?.Invoke(1.0, result.Success ? "NBackup completado" : "Error en NBackup");
        return result;
    }
    
    public static RepairResult NRestore(string binDir, string dbPath, string user, string password, string backupPath, ProgressCallback? onProgress = null)
    {
        onProgress?.Invoke(0.1, "Iniciando nrestore...");
        var result = RunTool(binDir, "nrestore", dbPath, user, password, backupPath);
        onProgress?.Invoke(1.0, result.Success ? "NRestore completado" : "Error en NRestore");
        return result;
    }
    
    public static RepairResult UpgradeODS(string binDir, string dbPath, string user, string password, ProgressCallback? onProgress = null)
    {
        onProgress?.Invoke(0.1, "Actualizando ODS (gfix -upgrade)...");
        var result = RunTool(binDir, "gfix", dbPath, user, password, "-upgrade");
        onProgress?.Invoke(1.0, result.Success ? "ODS actualizado" : "Error actualizando ODS");
        return result;
    }
    
    public static RepairResult MigrarODS(string binDir, string dbPath, string user, string password, string backupPath, ProgressCallback? onProgress = null)
    {
        onProgress?.Invoke(0.05, "Migrando ODS a Firebird 4.0 (backup gbak -b)...");
        var backup = RunTool(binDir, "gbak", dbPath, user, password, false, "-b", "-v", dbPath, backupPath);
        var output = new StringBuilder();
        output.AppendLine("=== BACKUP ===\n" + backup.Output);
        if (!backup.Success)
        {
            onProgress?.Invoke(1.0, "Error en backup");
            return new RepairResult { Success = false, Output = output.ToString(), Error = $"Backup falló: {backup.Error}", Step = "Migración ODS 3.0 → 4.0" };
        }
        
        onProgress?.Invoke(1.0, $"Backup completado. Restaure {backupPath} con Firebird 4.0 para generar la base (ODS 13).");
        return new RepairResult { Success = true, Output = output.ToString(), Step = "Migración ODS 3.0 → 4.0" };
    }
}