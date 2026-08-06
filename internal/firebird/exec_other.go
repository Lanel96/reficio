//go:build !windows

package firebird

import (
	"os/exec"
	"path/filepath"
)

func createCmd(binPath string, args []string) *exec.Cmd {
	return exec.Command(binPath, args...)
}

func getExeName(name string) string {
	return name
}

func joinBinPath(binDir, name string) string {
	return filepath.Join(binDir, name)
}
