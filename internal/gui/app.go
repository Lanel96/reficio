package gui

import (
	"fmt"
	"image/color"
	"os"
	"path/filepath"
	"strings"
	"time"

	"fyne.io/fyne/v2"
	"fyne.io/fyne/v2/canvas"
	"fyne.io/fyne/v2/container"
	"fyne.io/fyne/v2/dialog"
	"fyne.io/fyne/v2/layout"
	"fyne.io/fyne/v2/widget"

	"reficio/internal/config"
	"reficio/internal/firebird"
	"reficio/internal/theme"
	"reficio/internal/updater"
)

type App struct {
	fyneApp    fyne.App
	mainWindow fyne.Window
	config     config.AppConfig

	dbPathEntry   *widget.Entry
	userEntry     *widget.Entry
	passwordEntry *widget.Entry
	binDirEntry   *widget.Entry

	logText     *widget.Entry
	scrollLog   *container.Scroll
	progressBar *widget.ProgressBar
	statusLabel *widget.Label

	diagButton     *widget.Button
	repararButton  *widget.Button
	profundoButton *widget.Button
	backupButton   *widget.Button
	verifyButton   *widget.Button
	updateButton   *widget.Button

	correctionGUI *FacturaGUI
	pacienteGUI   *PacienteGUI
	tabs          *container.AppTabs

	running bool
	log     strings.Builder
}

func NewApp(fyneApp fyne.App, window fyne.Window) *App {
	return &App{
		fyneApp:    fyneApp,
		mainWindow: window,
		config:     config.Load(),
	}
}

func (a *App) Build() {
	content := a.buildUI()
	a.mainWindow.SetContent(content)
	a.loadConfigToUI()
}

func (a *App) SaveConfig() {
	a.config.LastDBPath = a.dbPathEntry.Text
	a.config.User = a.userEntry.Text
	a.config.Password = a.passwordEntry.Text
	a.config.BinDir = a.binDirEntry.Text
	_ = config.Save(a.config)
}

func (a *App) buildUI() fyne.CanvasObject {
	a.tabs = a.buildTabs()
	footer := a.buildFooter()

	topContent := container.NewVBox(
		a.buildLogo(),
		a.buildConnector(),
		widget.NewSeparator(),
		a.tabs,
	)

	return container.NewBorder(nil, footer, nil, nil, topContent)
}

func (a *App) buildLogo() fyne.CanvasObject {
	title := canvas.NewText("REFICIO", theme.ColorPrimary)
	title.TextSize = 20
	title.TextStyle = fyne.TextStyle{Bold: true}
	title.Alignment = fyne.TextAlignCenter

	subtitle := canvas.NewText("Reparador de Bases de Datos Firebird", color.NRGBA{R: 117, G: 117, B: 117, A: 255})
	subtitle.TextSize = 11
	subtitle.Alignment = fyne.TextAlignCenter

	return container.NewVBox(
		container.NewCenter(title),
		container.NewCenter(subtitle),
	)
}

func (a *App) buildConnector() fyne.CanvasObject {
	a.dbPathEntry = widget.NewEntry()
	a.dbPathEntry.SetPlaceHolder("Ruta del archivo .fdb...")

	browseBtn := widget.NewButton("Examinar", func() {
		dialog.ShowFileOpen(func(reader fyne.URIReadCloser, err error) {
			if err == nil && reader != nil {
				a.dbPathEntry.SetText(reader.URI().Path())
				reader.Close()
			}
		}, a.mainWindow)
	})

	a.userEntry = widget.NewEntry()
	a.userEntry.SetText("SYSDBA")
	a.passwordEntry = widget.NewPasswordEntry()
	a.passwordEntry.SetText("masterkey")

	a.binDirEntry = widget.NewEntry()
	a.binDirEntry.SetPlaceHolder("Firebird bin (opcional)...")

	connectBtn := widget.NewButton("CONECTAR", a.onConnect)
	connectBtn.Importance = widget.HighImportance

	row1 := container.NewHBox(
		widget.NewLabel("BD:"),
		container.NewGridWrap(fyne.NewSize(500, 36), a.dbPathEntry),
		browseBtn,
	)

	row2 := container.NewHBox(
		widget.NewLabel("User:"),
		container.NewGridWrap(fyne.NewSize(120, 36), a.userEntry),
		widget.NewLabel("Pass:"),
		container.NewGridWrap(fyne.NewSize(120, 36), a.passwordEntry),
		layout.NewSpacer(),
		connectBtn,
	)

	row3 := container.NewHBox(
		widget.NewLabel("Bin:"),
		container.NewGridWrap(fyne.NewSize(500, 36), a.binDirEntry),
	)

	return container.NewVBox(row1, row2, row3)
}

func (a *App) buildTabs() *container.AppTabs {
	module1 := a.buildRepairModule()
	module2 := a.buildFacturaModule()
	module3 := a.buildPacienteModule()

	tabs := container.NewAppTabs(
		container.NewTabItem("Reparacion BD", module1),
		container.NewTabItem("Corregir Factura", module2),
		container.NewTabItem("Corregir Paciente", module3),
	)
	tabs.SetTabLocation(container.TabLocationTop)
	return tabs
}

func (a *App) buildFacturaModule() fyne.CanvasObject {
	a.correctionGUI = NewFacturaGUI(a)
	return a.correctionGUI.Build()
}

func (a *App) buildPacienteModule() fyne.CanvasObject {
	a.pacienteGUI = NewPacienteGUI(a)
	return a.pacienteGUI.Build()
}

func (a *App) buildRepairModule() fyne.CanvasObject {
	a.diagButton = widget.NewButton("Diagnosticar", a.onDiagnosticar)
	a.diagButton.Importance = widget.HighImportance

	a.repararButton = widget.NewButton("Reparar Ligero", a.onRepararLigero)
	a.repararButton.Importance = widget.MediumImportance

	a.profundoButton = widget.NewButton("Reparar Profundo", a.onRepararProfundo)
	a.profundoButton.Importance = widget.DangerImportance

	a.backupButton = widget.NewButton("Backup", a.onSoloBackup)
	a.verifyButton = widget.NewButton("Verificar", a.onVerificar)

	buttonsRow := container.NewHBox(
		a.diagButton,
		a.repararButton,
		a.profundoButton,
		a.backupButton,
		a.verifyButton,
	)

	a.logText = widget.NewEntry()
	a.logText.Wrapping = fyne.TextWrapWord
	a.logText.MultiLine = true
	a.logText.Disable()
	a.logText.SetPlaceHolder("Esperando operacion...")

	a.scrollLog = container.NewVScroll(a.logText)
	a.scrollLog.SetMinSize(fyne.NewSize(0, 350))

	return container.NewVBox(buttonsRow, a.scrollLog)
}

func (a *App) buildFooter() fyne.CanvasObject {
	a.progressBar = widget.NewProgressBar()
	currentVer := updater.GetCurrentVersion()
	a.statusLabel = widget.NewLabel("Listo  (v" + currentVer + ")")
	a.updateButton = widget.NewButton("Buscar Actualización", a.onCheckUpdate)
	a.updateButton.Importance = widget.LowImportance

	return container.NewHBox(a.statusLabel, a.progressBar, a.updateButton)
}

func (a *App) loadConfigToUI() {
	if a.config.LastDBPath != "" {
		a.dbPathEntry.SetText(a.config.LastDBPath)
	}
	if a.config.User != "" {
		a.userEntry.SetText(a.config.User)
	}
	if a.config.Password != "" {
		a.passwordEntry.SetText(a.config.Password)
	}
	if a.config.BinDir != "" {
		a.binDirEntry.SetText(a.config.BinDir)
	}
}

func (a *App) getConfig() firebird.Config {
	return firebird.Config{
		DBPath:   a.dbPathEntry.Text,
		User:     a.userEntry.Text,
		Password: a.passwordEntry.Text,
		BinDir:   a.binDirEntry.Text,
	}
}

func (a *App) validateDBPath() bool {
	if a.dbPathEntry.Text == "" {
		dialog.ShowError(fmt.Errorf("seleccione una base de datos"), a.mainWindow)
		return false
	}
	if !strings.HasSuffix(strings.ToLower(a.dbPathEntry.Text), ".fdb") {
		dialog.ShowError(fmt.Errorf("extension .fdb requerida"), a.mainWindow)
		return false
	}
	return true
}

func (a *App) setRunning(running bool) {
	a.running = running
	a.diagButton.Disable()
	a.repararButton.Disable()
	a.profundoButton.Disable()
	a.backupButton.Disable()
	a.verifyButton.Disable()

	if !running {
		a.diagButton.Enable()
		a.repararButton.Enable()
		a.profundoButton.Enable()
		a.backupButton.Enable()
		a.verifyButton.Enable()
	}
}

func (a *App) updateProgress(progress float64, message string) {
	fyne.Do(func() {
		a.progressBar.SetValue(progress)
		a.statusLabel.SetText(message)
	})
}

func (a *App) appendLog(msg string) {
	timestamp := time.Now().Format("15:04:05")
	a.log.WriteString(fmt.Sprintf("[%s] %s\n", timestamp, msg))
	a.logText.SetText(a.log.String())
	a.scrollLog.ScrollToBottom()
}

func (a *App) appendLogResult(result firebird.RepairResult) {
	icon := "[OK]"
	if !result.Success {
		icon = "[FAIL]"
	}
	a.appendLog(fmt.Sprintf("%s %s: %s", icon, result.Step, a.getResultSummary(result)))

	if result.Output != "" {
		lines := strings.Split(result.Output, "\n")
		if len(lines) > 15 {
			lines = lines[:15]
		}
		for _, line := range lines {
			if strings.TrimSpace(line) != "" {
				a.appendLog("  " + line)
			}
		}
	}
}

func (a *App) getResultSummary(result firebird.RepairResult) string {
	if result.Error != nil {
		return result.Error.Error()
	}
	return "OK"
}

func (a *App) onCheckUpdate() {
	info, err := updater.CheckForUpdate()
	if err != nil {
		a.appendLog(fmt.Sprintf("[ERROR] No se pudo verificar actualización: %v", err))
		// Show setup instructions for private repo
		dialog.ShowInformation("Configurar credenciales",
			"El repositorio es privado. Para usar actualizaciones:\n\n"+
				"1. Crear archivo en: %USERPROFILE%/.git-credentials\n"+
				"2. Contenido: https://tu_usuario:tu_token@git.upc.com.mx\n\n"+
				"3. O usar variables de entorno:\n"+
				"   GIT_USERNAME=tu_usuario\n"+
				"   GIT_PASSWORD=tu_token\n\n"+
				"4. Ejecutar Reficio_setup_creds.bat para asistencia",
			a.mainWindow)
		return
	}

	if !info.Available {
		dialog.ShowInformation("Actualización", fmt.Sprintf("Ya tienes la última versión (v%s).", info.CurrentVersion), a.mainWindow)
		return
	}

	dialog.ShowConfirm(
		"Actualización disponible",
		fmt.Sprintf("Versión actual: v%s\nNueva versión: v%s\n\n¿Descargar e instalar?", info.CurrentVersion, info.LatestVersion),
		func(confirmed bool) {
			if !confirmed {
				return
			}

			progress := dialog.NewProgress("Descargando actualización", "Descargando...", a.mainWindow)
			progress.Resize(fyne.NewSize(400, 120))
			progress.Show()

			go func() {
				err := updater.DownloadAndInstallUpdate()
				progress.Hide()

				if err != nil {
					dialog.ShowError(err, a.mainWindow)
					return
				}

				dialog.ShowConfirm(
					"Actualización completa",
					"La actualización ha sido instalada. ¿Reiniciar ahora?",
					func(restart bool) {
						if restart {
							a.fyneApp.Quit()
						}
					},
					a.mainWindow,
				)
			}()
		},
		a.mainWindow,
	)
}

func (a *App) onConnect() {
	if !a.validateDBPath() {
		return
	}

	a.statusLabel.SetText("Conectando...")
	cfg := a.getConfig()

	if err := a.correctionGUI.ConnectDB(cfg); err != nil {
		dialog.ShowError(err, a.mainWindow)
		a.statusLabel.SetText("Error")
		return
	}

	if err := a.pacienteGUI.ConnectDB(cfg); err != nil {
		dialog.ShowError(err, a.mainWindow)
		a.statusLabel.SetText("Error")
		return
	}

	a.statusLabel.SetText("Conectado")
	a.appendLog("Conexion OK")
}

func (a *App) onDiagnosticar() {
	if !a.validateDBPath() {
		return
	}

	a.setRunning(true)
	a.progressBar.SetValue(0)
	a.appendLog("Iniciando diagnostico...")

	go func() {
		result := firebird.Diagnosticar(a.getConfig(), a.updateProgress)
		fyne.Do(func() {
			a.appendLogResult(result)
			a.setRunning(false)
		})
	}()
}

func (a *App) onRepararLigero() {
	if !a.validateDBPath() {
		return
	}

	a.setRunning(true)
	a.progressBar.SetValue(0)
	a.appendLog("Iniciando reparacion ligera...")

	go func() {
		result := firebird.RepararLigero(a.getConfig(), a.updateProgress)
		fyne.Do(func() {
			a.appendLogResult(result)
			a.setRunning(false)
		})
	}()
}

func (a *App) onRepararProfundo() {
	if !a.validateDBPath() {
		return
	}

	dialog.ShowConfirm("Confirmar",
		"Esto creara backup y restaurara la BD.\nContinuar?",
		func(ok bool) {
			if ok {
				a.ejecutarReparacionProfunda()
			}
		}, a.mainWindow)
}

func (a *App) ejecutarReparacionProfunda() {
	a.setRunning(true)
	a.progressBar.SetValue(0)
	a.appendLog("Reparacion profunda...")

	cfg := a.getConfig()
	timestamp := time.Now().Format("20060102_150405")
	backupPath := cfg.DBPath + "." + timestamp + ".fbk"

	// Renombrar original como dañada
	ext := filepath.Ext(cfg.DBPath)
	base := strings.TrimSuffix(cfg.DBPath, ext)
	danadaPath := base + "_dañada" + ext

	a.appendLog(fmt.Sprintf("Original se renombrara a: %s", danadaPath))
	a.appendLog(fmt.Sprintf("BD reparada tendra nombre original: %s", cfg.DBPath))

	go func() {
		// Ejecutar backup + restore
		result := firebird.RepararProfundo(cfg, backupPath, cfg.DBPath, a.updateProgress)

		if result.Success {
			// Renombrar original como dañada
			fyne.Do(func() {
				a.updateProgress(0.9, "Renombrando BD dañada...")
			})

			err := os.Rename(cfg.DBPath, danadaPath)
			if err != nil {
				result.Error = fmt.Errorf("no se pudo renombrar original: %w", err)
				result.Success = false
			} else {
				a.appendLog(fmt.Sprintf("Original renombrada a: %s", danadaPath))
			}
		}

		fyne.Do(func() {
			a.appendLogResult(result)
			a.setRunning(false)
		})
	}()
}

func (a *App) onSoloBackup() {
	if !a.validateDBPath() {
		return
	}

	a.setRunning(true)
	a.progressBar.SetValue(0)
	a.appendLog("Iniciando backup...")

	cfg := a.getConfig()
	timestamp := time.Now().Format("20060102_150405")
	backupPath := cfg.DBPath + "." + timestamp + ".fbk"

	go func() {
		result := firebird.SoloBackup(cfg, backupPath, a.updateProgress)
		fyne.Do(func() {
			a.appendLogResult(result)
			a.setRunning(false)
		})
	}()
}

func (a *App) onVerificar() {
	if !a.validateDBPath() {
		return
	}

	a.setRunning(true)
	a.progressBar.SetValue(0)
	a.appendLog("Verificando integridad...")

	go func() {
		result := firebird.VerificarIntegridad(a.getConfig(), a.updateProgress)
		fyne.Do(func() {
			a.appendLogResult(result)
			a.setRunning(false)
		})
	}()
}
