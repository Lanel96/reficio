package main

import (
	"fmt"
	"log"
	"time"

	_ "embed"

	"fyne.io/fyne/v2"
	"fyne.io/fyne/v2/app"
	"fyne.io/fyne/v2/dialog"

	"reficio/internal/gui"
	"reficio/internal/theme"
	"reficio/internal/updater"
)

//go:embed logo.png
var logoPNG []byte

//go:embed VERSION
var versionStr string

func main() {
	updater.SetEmbeddedVersion(versionStr)

	myApp := app.NewWithID("com.reficio.firebird-repair")
	myApp.Settings().SetTheme(theme.NewReficioTheme())

	myWindow := myApp.NewWindow("Reficio")
	myWindow.Resize(fyne.NewSize(900, 680))
	myWindow.CenterOnScreen()

	// Set window icon
	iconResource := fyne.NewStaticResource("logo.png", logoPNG)
	myWindow.SetIcon(iconResource)

	// Show current version in window title
	currentVer := updater.GetCurrentVersion()
	myWindow.SetTitle(fmt.Sprintf("Reficio v%s", currentVer))

	// Build UI
	appGUI := gui.NewApp(myApp, myWindow)
	appGUI.Build()

	// Check for updates in background
	go func() {
		time.Sleep(time.Second * 2)
		checkForUpdates(myApp, myWindow)
	}()

	myWindow.ShowAndRun()
	appGUI.SaveConfig()
}

func checkForUpdates(myApp fyne.App, myWindow fyne.Window) {
	if !updater.IsNewerThanBinary() {
		return
	}

	info, err := updater.CheckForUpdate()
	if err != nil {
		log.Printf("Error checking for update: %v", err)
		return
	}

	if !info.Available {
		return
	}

	dialog.ShowConfirm(
		"Actualización disponible",
		fmt.Sprintf("Versión actual: v%s\nNueva versión: v%s\n\n¿Descargar e instalar?", info.CurrentVersion, info.LatestVersion),
		dialogConfirmUpdate(myApp, myWindow, info),
		myWindow,
	)
}

func dialogConfirmUpdate(myApp fyne.App, myWindow fyne.Window, info *updater.UpdateInfo) func(bool) {
	return func(confirmed bool) {
		if !confirmed {
			return
		}

		progress := dialog.NewProgress("Descargando actualización", "Descargando...", myWindow)
		progress.Resize(fyne.NewSize(400, 120))
		progress.Show()

		go func() {
			err := updater.DownloadAndInstallUpdate()
			progress.Hide()
			if err != nil {
				dialog.ShowError(err, myWindow)
				return
			}

			dialog.ShowConfirm(
				"Actualización completa",
				"La actualización ha sido instalada. ¿Reiniciar ahora?",
				func(restart bool) {
					if restart {
						myApp.Quit()
					}
				},
				myWindow,
			)
		}()
	}
}
