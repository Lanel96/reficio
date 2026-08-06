package config

import (
	"encoding/json"
	"os"
	"path/filepath"
)

// AppConfig configuración persistente de la aplicación
type AppConfig struct {
	LastDBPath string `json:"last_db_path"`
	User       string `json:"user"`
	Password   string `json:"password"`
	BinDir     string `json:"bin_dir"`
	WindowX    int    `json:"window_x"`
	WindowY    int    `json:"window_y"`
}

// DefaultConfig retorna configuración por defecto
func DefaultConfig() AppConfig {
	return AppConfig{
		User:     "SYSDBA",
		Password: "masterkey",
	}
}

// GetConfigPath retorna la ruta del archivo de configuración
func GetConfigPath() string {
	home, err := os.UserHomeDir()
	if err != nil {
		return "reficio-config.json"
	}
	return filepath.Join(home, ".reficio", "config.json")
}

// Load carga la configuración desde disco
func Load() AppConfig {
	cfg := DefaultConfig()
	
	path := GetConfigPath()
	data, err := os.ReadFile(path)
	if err != nil {
		return cfg
	}
	
	_ = json.Unmarshal(data, &cfg)
	return cfg
}

// Save guarda la configuración a disco
func Save(cfg AppConfig) error {
	path := GetConfigPath()
	
	// Crear directorio si no existe
	dir := filepath.Dir(path)
	if err := os.MkdirAll(dir, 0755); err != nil {
		return err
	}
	
	data, err := json.MarshalIndent(cfg, "", "  ")
	if err != nil {
		return err
	}
	
	return os.WriteFile(path, data, 0644)
}
