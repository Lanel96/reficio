package theme

import (
	"image/color"

	"fyne.io/fyne/v2"
	"fyne.io/fyne/v2/canvas"
	"fyne.io/fyne/v2/container"
)

var (
	LogoPrimary   = color.NRGBA{R: 79, G: 140, B: 255, A: 255}
	LogoSecondary = color.NRGBA{R: 124, G: 77, B: 255, A: 255}
	LogoWhite     = color.NRGBA{R: 255, G: 255, B: 255, A: 255}
)

func NewLogo() fyne.CanvasObject {
	title := canvas.NewText("REFICIO", LogoPrimary)
	title.TextSize = 26
	title.TextStyle = fyne.TextStyle{Bold: true}
	title.Alignment = fyne.TextAlignCenter

	subtitle := canvas.NewText("Reparador de Bases de Datos Firebird", color.NRGBA{R: 100, G: 100, B: 100, A: 255})
	subtitle.TextSize = 11
	subtitle.Alignment = fyne.TextAlignCenter

	return container.NewVBox(
		container.NewCenter(title),
		container.NewCenter(subtitle),
	)
}
