package firebird

import (
	"os/exec"
	"path/filepath"
	"syscall"
)

func createCmd(binPath string, args []string) *exec.Cmd {
	cmd := exec.Command(binPath, args...)
	cmd.SysProcAttr = &syscall.SysProcAttr{
		HideWindow:    true,
		CreationFlags: 0x08000000, // CREATE_NO_WINDOW
	}
	return cmd
}

func getExeName(name string) string {
	return name + ".exe"
}

func joinBinPath(binDir, name string) string {
	return filepath.Join(binDir, name+".exe")
}
