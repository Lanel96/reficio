package firebird

import (
	"fmt"
	"os/exec"
	"strings"
)

// Config contiene la configuración de las herramientas Firebird
type Config struct {
	DBPath   string
	User     string
	Password string
	BinDir   string
}

// RepairResult resultado de una operación de reparación
type RepairResult struct {
	Success  bool
	Output   string
	Error    error
	Step     string
}

// ProgressCallback función para reportar progreso
type ProgressCallback func(progress float64, message string)

// ToolNames nombres de las herramientas Firebird
var ToolNames = map[string]string{
	"gstat": "gstat",
	"gfix":  "gfix",
	"gbak":  "gbak",
}

// ValidateTools verifica que las herramientas estén disponibles
func ValidateTools(binDir string) map[string]bool {
	tools := make(map[string]bool)

	for name := range ToolNames {
		binName := getExeName(name)

		binPath := binName
		if binDir != "" {
			binPath = joinBinPath(binDir, name)
		}

		_, err := exec.LookPath(binPath)
		if err != nil {
			if binDir == "" {
				_, err = exec.LookPath(name)
			}
		}
		tools[name] = err == nil
	}

	return tools
}

// RunTool ejecuta una utilidad de Firebird sin mostrar consola
func RunTool(cfg Config, tool string, args ...string) (string, error) {
	binName := getExeName(tool)

	binPath := binName
	if cfg.BinDir != "" {
		binPath = joinBinPath(cfg.BinDir, tool)
	}

	cmd := createCmd(binPath, args)
	out, err := cmd.CombinedOutput()
	return string(out), err
}

// Diagnosticar ejecuta gstat para diagnosticar el estado de la BD
func Diagnosticar(cfg Config, onProgress ProgressCallback) RepairResult {
	result := RepairResult{Step: "Diagnóstico"}

	if onProgress != nil {
		onProgress(0.1, "Iniciando gstat...")
	}

	output, err := RunTool(cfg, "gstat", "-header", cfg.DBPath)
	result.Output = output

	if onProgress != nil {
		onProgress(0.8, "Analizando resultados...")
	}

	if err != nil {
		result.Error = fmt.Errorf("gstat falló: %w", err)
		result.Success = false
		if onProgress != nil {
			onProgress(1.0, "Error en diagnostico")
		}
		return result
	}

	lower := strings.ToLower(output)
	if strings.Contains(lower, "damaged") || strings.Contains(lower, "corrupt") {
		result.Error = fmt.Errorf("se detectó daño en la base de datos")
		result.Success = false
	} else {
		result.Success = true
	}

	if onProgress != nil {
		onProgress(1.0, "Diagnostico completado")
	}

	return result
}

// RepararLigero ejecuta gfix -validate -full
func RepararLigero(cfg Config, onProgress ProgressCallback) RepairResult {
	result := RepairResult{Step: "Reparación Ligera"}

	if onProgress != nil {
		onProgress(0.1, "Iniciando gfix...")
	}

	output, err := RunTool(cfg, "gfix",
		"-validate", "-full",
		"-user", cfg.User, "-password", cfg.Password,
		cfg.DBPath,
	)
	result.Output = output

	if onProgress != nil {
		onProgress(0.9, "Finalizando...")
	}

	if err != nil {
		result.Error = fmt.Errorf("gfix falló: %w", err)
		result.Success = false
	} else {
		result.Success = true
	}

	if onProgress != nil {
		onProgress(1.0, "Reparacion completada")
	}

	return result
}

// RepararProfundo ejecuta backup + restore con gbak
func RepararProfundo(cfg Config, backupPath, restoredPath string, onProgress ProgressCallback) RepairResult {
	result := RepairResult{Step: "Reparación Profunda"}

	// Paso 1: Backup
	if onProgress != nil {
		onProgress(0.1, "Generando backup...")
	}

	backupOut, err := RunTool(cfg, "gbak",
		"-b", "-g", "-v",
		"-user", cfg.User, "-password", cfg.Password,
		cfg.DBPath, backupPath,
	)
	result.Output = "Backup:\n" + backupOut

	if err != nil {
		result.Error = fmt.Errorf("backup falló: %w", err)
		result.Success = false
		if onProgress != nil {
			onProgress(1.0, "Error en backup")
		}
		return result
	}

	if onProgress != nil {
		onProgress(0.5, "Backup completado, iniciando restore...")
	}

	// Paso 2: Restore
	restoreOut, err := RunTool(cfg, "gbak",
		"-c", "-v",
		"-user", cfg.User, "-password", cfg.Password,
		backupPath, restoredPath,
	)
	result.Output += "\nRestore:\n" + restoreOut

	if err != nil {
		result.Error = fmt.Errorf("restauración falló: %w", err)
		result.Success = false
	} else {
		result.Success = true
	}

	if onProgress != nil {
		onProgress(1.0, "Reparacion completada")
	}

	return result
}

// SoloBackup ejecuta gbak -b sin restaurar
func SoloBackup(cfg Config, backupPath string, onProgress ProgressCallback) RepairResult {
	result := RepairResult{Step: "Backup"}

	if onProgress != nil {
		onProgress(0.1, "Iniciando backup...")
	}

	output, err := RunTool(cfg, "gbak",
		"-b", "-g", "-v",
		"-user", cfg.User, "-password", cfg.Password,
		cfg.DBPath, backupPath,
	)
	result.Output = output

	if err != nil {
		result.Error = fmt.Errorf("backup falló: %w", err)
		result.Success = false
	} else {
		result.Success = true
	}

	if onProgress != nil {
		onProgress(1.0, "Backup completado")
	}

	return result
}

// VerificarIntegridad ejecuta gstat para verificar después de reparación
func VerificarIntegridad(cfg Config, onProgress ProgressCallback) RepairResult {
	result := RepairResult{Step: "Verificación"}

	if onProgress != nil {
		onProgress(0.1, "Verificando integridad...")
	}

	output, err := RunTool(cfg, "gstat", "-header", cfg.DBPath)
	result.Output = output

	if onProgress != nil {
		onProgress(0.9, "Analizando...")
	}

	if err != nil {
		result.Error = fmt.Errorf("verificación falló: %w", err)
		result.Success = false
	} else {
		result.Success = true
	}

	if onProgress != nil {
		onProgress(1.0, "Verificacion completada")
	}

	return result
}
