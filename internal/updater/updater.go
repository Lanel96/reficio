package updater

import (
	"bytes"
	"encoding/base64"
	"encoding/json"
	"fmt"
	"io"
	"net/http"
	"os"
	"path/filepath"
	"runtime"
	"strconv"
	"strings"
	"time"
)

const (
	GitRepoURL  = "http://git.upc.com.mx/luisleon/reficio.git"
	ApiBaseURL  = "https://git.upc.com.mx/luisleon/reficio"
	VersionFile = "VERSION"
	TempDir     = "reficio_update"
)

type UpdateInfo struct {
	CurrentVersion string
	LatestVersion  string
	Available      bool
	DownloadURL    string
}

type GitLabTag struct {
	Name string `json:"name"`
}

func GetCurrentVersion() string {
	// Try embedded version first (set via SetEmbeddedVersion)
	if embeddedVersion != "" {
		return embeddedVersion
	}

	// Try multiple locations for VERSION file
	paths := []string{
		VersionFile,
		filepath.Join(filepath.Dir(mustGetExecutable()), VersionFile),
	}

	for _, path := range paths {
		data, err := os.ReadFile(path)
		if err == nil {
			return strings.TrimSpace(string(data))
		}
	}
	return "0.0.0"
}

// embeddedVersion can be set via SetEmbeddedVersion() during package init
var embeddedVersion string

func SetEmbeddedVersion(v string) {
	embeddedVersion = strings.TrimSpace(v)
}

func mustGetExecutable() string {
	path, err := os.Executable()
	if err != nil {
		return ""
	}
	return path
}

func getAuthCredentials() (string, error) {
	// Try reading from .git-credentials file first
	homeDir, err := os.UserHomeDir()
	if err == nil {
		credPath := filepath.Join(homeDir, ".git-credentials")
		data, err := os.ReadFile(credPath)
		if err == nil {
			lines := strings.Split(string(data), "\n")
			for _, line := range lines {
				if strings.Contains(line, "git.upc.com.mx") && strings.Contains(line, "://") {
					// Parse URL: https://username:password@git.upc.com.mx
					remaining := strings.SplitN(line, "://", 2)
					if len(remaining) == 2 {
						authPart := strings.SplitN(remaining[1], "@", 2)
						if len(authPart) == 2 {
							return authPart[0], nil
						}
					}
				}
			}
		}
	}

	// Fallback: try environment variables
	username := os.Getenv("GIT_USERNAME")
	password := os.Getenv("GIT_PASSWORD")
	if username != "" && password != "" {
		return username + ":" + password, nil
	}

	return "", fmt.Errorf("no se encontraron credenciales")
}

func getLatestTag() (string, error) {
	// Use GitLab API with HTTP (no git dependency)
	apiURL := ApiBaseURL + "/-/tags?per_page=20&orderby=created_at&sort=desc"

	req, err := http.NewRequest("GET", apiURL, nil)
	if err != nil {
		return "", err
	}

	// Set auth if credentials available
	if creds, err := getAuthCredentials(); err == nil && creds != "" {
		req.Header.Set("Authorization", "Basic "+base64.StdEncoding.EncodeToString([]byte(creds)))
	}

	resp, err := http.DefaultClient.Do(req)
	if err != nil {
		return "", fmt.Errorf("error al consultar API: %w", err)
	}
	defer resp.Body.Close()

	if resp.StatusCode != http.StatusOK {
		return "", fmt.Errorf("API respondió con código: %s (%s)", resp.Status, apiURL)
	}

	body, err := io.ReadAll(resp.Body)
	if err != nil {
		return "", fmt.Errorf("error al leer respuesta: %w", err)
	}

	// Check if response is HTML (error page) instead of JSON
	if bytes.Contains(body, []byte("<html")) || bytes.Contains(body, []byte("<HTML")) {
		return "", fmt.Errorf("respuesta no es JSON (posible página de login). Configura credenciales en ~/.git-credentials o variables de entorno GIT_USERNAME/GIT_PASSWORD")
	}

	var tags []GitLabTag

	if err := json.Unmarshal(body, &tags); err != nil {
		return "", fmt.Errorf("error al parsear respuesta: %w", err)
	}

	if len(tags) == 0 {
		return "", fmt.Errorf("no se encontraron tags")
	}

	// Find the latest version tag
	var latestTag string
	for _, tag := range tags {
		tagName := tag.Name
		// Remove 'v' prefix for comparison
		if strings.HasPrefix(tagName, "v") {
			tagName = tagName[1:]
		}
		if latestTag == "" || compareVersions(tagName, latestTag) > 0 {
			latestTag = tagName
		}
	}

	if latestTag == "" {
		return "", fmt.Errorf("no se encontraron tags válidos")
	}

	return latestTag, nil
}

func compareVersions(v1, v2 string) int {
	v1Parts := strings.Split(v1, ".")
	v2Parts := strings.Split(v2, ".")

	maxLen := len(v1Parts)
	if len(v2Parts) > maxLen {
		maxLen = len(v2Parts)
	}

	for i := 0; i < maxLen; i++ {
		var n1, n2 int
		if i < len(v1Parts) {
			n1, _ = strconv.Atoi(v1Parts[i])
		}
		if i < len(v2Parts) {
			n2, _ = strconv.Atoi(v2Parts[i])
		}
		if n1 > n2 {
			return 1
		}
		if n1 < n2 {
			return -1
		}
	}
	return 0
}

func CheckForUpdate() (*UpdateInfo, error) {
	currentVersion := GetCurrentVersion()
	latestVersion, err := getLatestTag()
	if err != nil {
		return nil, err
	}

	available := compareVersions(latestVersion, currentVersion) > 0

	return &UpdateInfo{
		CurrentVersion: currentVersion,
		LatestVersion:  latestVersion,
		Available:      available,
		DownloadURL:    fmt.Sprintf("%s/-/raw/main/reficio.exe", ApiBaseURL),
	}, nil
}

func DownloadAndInstallUpdate() error {
	// Download binary based on OS
	var binaryName string
	if runtime.GOOS == "windows" {
		binaryName = "reficio.exe"
	} else {
		binaryName = "reficio"
	}

	// Create temp directory
	tmpDir, err := os.MkdirTemp("", TempDir)
	if err != nil {
		return err
	}
	defer os.RemoveAll(tmpDir)

	// Download from GitLab
	downloadURL := fmt.Sprintf("%s/-/raw/main/%s", ApiBaseURL, binaryName)

	req, err := http.NewRequest("GET", downloadURL, nil)
	if err != nil {
		return err
	}

	// Set auth if credentials available
	if creds, err := getAuthCredentials(); err == nil && creds != "" {
		req.Header.Set("Authorization", "Basic "+base64.StdEncoding.EncodeToString([]byte(creds)))
	}

	resp, err := http.DefaultClient.Do(req)
	if err != nil {
		return err
	}
	defer resp.Body.Close()

	if resp.StatusCode != http.StatusOK {
		return fmt.Errorf("error en la descarga: %s", resp.Status)
	}

	err = downloadFile(resp, filepath.Join(tmpDir, binaryName))
	if err != nil {
		return err
	}

	// Replace current binary
	execPath, err := os.Executable()
	if err != nil {
		return err
	}

	// On Windows, we need to handle the rename differently
	if runtime.GOOS == "windows" {
		backupPath := execPath + ".old"
		os.Rename(execPath, backupPath)
		defer os.Remove(backupPath)
	}

	srcInfo, err := os.Stat(filepath.Join(tmpDir, binaryName))
	if err != nil {
		return err
	}

	if err := copyFile(filepath.Join(tmpDir, binaryName), execPath); err != nil {
		return err
	}

	// Ensure the file is executable
	if runtime.GOOS != "windows" {
		os.Chmod(execPath, srcInfo.Mode())
	}

	return nil
}

func downloadFile(resp *http.Response, dest string) error {
	// Create the file
	out, err := os.Create(dest)
	if err != nil {
		return err
	}
	defer out.Close()

	// Write the body to file
	_, err = io.Copy(out, resp.Body)
	if err != nil {
		return err
	}

	return nil
}

func copyFile(src, dst string) error {
	sourceFileStat, err := os.Stat(src)
	if err != nil {
		return err
	}

	if !sourceFileStat.Mode().IsRegular() {
		return fmt.Errorf("%s no es un archivo regular", src)
	}

	source, err := os.Open(src)
	if err != nil {
		return err
	}
	defer source.Close()

	destination, err := os.Create(dst)
	if err != nil {
		return err
	}
	defer destination.Close()

	_, err = io.Copy(destination, source)
	return err
}

func IsNewerThanBinary() bool {
	execPath, err := os.Executable()
	if err != nil {
		return false
	}

	fileInfo, err := os.Stat(execPath)
	if err != nil {
		return false
	}

	age := time.Since(fileInfo.ModTime())
	return age > time.Hour
}
