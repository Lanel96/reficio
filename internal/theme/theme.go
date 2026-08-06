package theme

import (
	"image/color"

	"fyne.io/fyne/v2"
	"fyne.io/fyne/v2/theme"
)

var (
	ColorPrimary       = color.NRGBA{R: 33, G: 150, B: 243, A: 255}
	ColorPrimaryLight  = color.NRGBA{R: 100, G: 181, B: 246, A: 255}
	ColorPrimaryDark   = color.NRGBA{R: 25, G: 118, B: 210, A: 255}
	ColorAccent        = color.NRGBA{R: 0, G: 188, B: 212, A: 255}
	ColorBackground    = color.NRGBA{R: 250, G: 250, B: 250, A: 255}
	ColorSurface       = color.NRGBA{R: 255, G: 255, B: 255, A: 255}
	ColorText          = color.NRGBA{R: 33, G: 33, B: 33, A: 255}
	ColorTextSecondary = color.NRGBA{R: 117, G: 117, B: 117, A: 255}
	ColorBorder        = color.NRGBA{R: 224, G: 224, B: 224, A: 255}
	ColorSuccess       = color.NRGBA{R: 76, G: 175, B: 80, A: 255}
	ColorWarning       = color.NRGBA{R: 255, G: 152, B: 0, A: 255}
	ColorError         = color.NRGBA{R: 244, G: 67, B: 54, A: 255}
	ColorOverlay       = color.NRGBA{R: 255, G: 255, B: 255, A: 255}
)

type ReficioTheme struct{}

func NewReficioTheme() *ReficioTheme {
	return &ReficioTheme{}
}

func (t *ReficioTheme) Color(name fyne.ThemeColorName, variant fyne.ThemeVariant) color.Color {
	switch name {
	case theme.ColorNameBackground:
		return ColorBackground
	case theme.ColorNameForeground:
		return ColorText
	case theme.ColorNamePlaceHolder:
		return ColorTextSecondary
	case theme.ColorNameButton:
		return ColorPrimary
	case theme.ColorNameDisabledButton:
		return color.NRGBA{R: 189, G: 189, B: 189, A: 255}
	case theme.ColorNameHover:
		return color.NRGBA{R: 33, G: 150, B: 243, A: 30}
	case theme.ColorNamePressed:
		return color.NRGBA{R: 33, G: 150, B: 243, A: 50}
	case theme.ColorNameInputBackground:
		return ColorSurface
	case theme.ColorNameInputBorder:
		return ColorBorder
	case theme.ColorNameDisabled:
		return color.NRGBA{R: 189, G: 189, B: 189, A: 255}
	case theme.ColorNameSeparator:
		return ColorBorder
	case theme.ColorNameSuccess:
		return ColorSuccess
	case theme.ColorNameWarning:
		return ColorWarning
	case theme.ColorNameError:
		return ColorError
	case theme.ColorNameOverlayBackground:
		return ColorOverlay
	default:
		// Forzar uso de colores claros en todos los demás casos
		return theme.DefaultTheme().Color(name, theme.VariantLight)
	}
}

func (t *ReficioTheme) Font(style fyne.TextStyle) fyne.Resource {
	return theme.DefaultTheme().Font(style)
}

func (t *ReficioTheme) Icon(name fyne.ThemeIconName) fyne.Resource {
	return theme.DefaultTheme().Icon(name)
}

func (t *ReficioTheme) Size(name fyne.ThemeSizeName) float32 {
	return theme.DefaultTheme().Size(name)
}
