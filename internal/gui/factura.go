package gui

import (
	"fmt"

	"fyne.io/fyne/v2"
	"fyne.io/fyne/v2/container"
	"fyne.io/fyne/v2/dialog"
	"fyne.io/fyne/v2/layout"
	"fyne.io/fyne/v2/widget"

	"reficio/internal/firebird"
	"reficio/internal/modules"
)

type FacturaGUI struct {
	app         *App
	module      *modules.CorrectionModule
	db          *firebird.DBConnection
	codiEntry   *widget.Entry
	resultList  *widget.List
	statusLabel *widget.Label
	countLabel  *widget.Label
	records     []map[string]interface{}
	columns     []string
	selectedRow int
}

// Fields to display in the editor
var FacturaFields = []string{"CODI", "NOMBRECI", "USOCFDI", "REGIFISC"}

func NewFacturaGUI(app *App) *FacturaGUI {
	return &FacturaGUI{
		app:         app,
		selectedRow: -1,
	}
}

func (g *FacturaGUI) Build() fyne.CanvasObject {
	g.statusLabel = widget.NewLabel("Conecte a una BD")
	g.countLabel = widget.NewLabel("0 registros")

	g.codiEntry = widget.NewEntry()
	g.codiEntry.SetPlaceHolder("Ingrese CODI...")

	searchBtn := widget.NewButton("Buscar", g.onSearch)
	searchBtn.Importance = widget.HighImportance

	clearBtn := widget.NewButton("Limpiar", g.onClear)

	searchRow := container.NewHBox(
		widget.NewLabel("CODI:"),
		container.NewGridWrap(fyne.NewSize(300, 36), g.codiEntry),
		searchBtn,
		clearBtn,
	)

	g.resultList = widget.NewList(
		func() int {
			return len(g.records)
		},
		func() fyne.CanvasObject {
			return widget.NewLabel("----------")
		},
		func(id widget.ListItemID, obj fyne.CanvasObject) {
			label := obj.(*widget.Label)
			if id < len(g.records) {
				record := g.records[id]
				codi := record["CODI"]
				nombreci := record["NOMBRECI"]
				usoCFDI := record["USOCFDI"]
				label.SetText(fmt.Sprintf("CODI: %v | NOMBRE: %v | USOCFDI: %v", codi, nombreci, usoCFDI))
			}
		},
	)

	g.resultList.OnSelected = func(id widget.ListItemID) {
		g.selectedRow = id
	}

	scrollList := container.NewVScroll(g.resultList)
	scrollList.SetMinSize(fyne.NewSize(0, 280))

	editBtn := widget.NewButton("Editar", g.onEdit)
	editBtn.Importance = widget.HighImportance

	buttonsRow := container.NewHBox(
		editBtn,
		layout.NewSpacer(),
		g.countLabel,
		g.statusLabel,
	)

	return container.NewVBox(
		searchRow,
		widget.NewSeparator(),
		scrollList,
		buttonsRow,
	)
}

func (g *FacturaGUI) ConnectDB(cfg firebird.Config) error {
	g.db = firebird.NewDBConnection(cfg)
	if err := g.db.TestConnection(); err != nil {
		return err
	}
	g.loadFacturaModule()
	return nil
}

func (g *FacturaGUI) loadFacturaModule() {
	columns, err := g.db.GetColumns("DINGR")
	if err != nil {
		g.statusLabel.SetText("Error al cargar DINGR")
		return
	}

	g.columns = columns
	g.module = modules.NewCorrectionModule(g.db, "DINGR")
	g.module.Columns = columns

	count, err := g.module.GetRecordCount()
	if err == nil {
		g.statusLabel.SetText("DINGR")
		g.countLabel.SetText(fmt.Sprintf("%d registros", count))
	}
}

func (g *FacturaGUI) onSearch() {
	if g.module == nil {
		return
	}

	codi := g.codiEntry.Text

	if codi == "" {
		return
	}

	result, err := g.module.SearchExact("CODI", codi)
	if err != nil {
		dialog.ShowError(err, g.app.mainWindow)
		return
	}

	g.records = result.Records
	g.selectedRow = -1
	g.resultList.Refresh()
	g.countLabel.SetText(fmt.Sprintf("%d resultados", result.Count))
	g.statusLabel.SetText("Haga clic en un registro para editarlo")
}

func (g *FacturaGUI) onClear() {
	g.codiEntry.SetText("")
	g.records = nil
	g.selectedRow = -1
	g.resultList.Refresh()
	g.countLabel.SetText("0 registros")
}

func (g *FacturaGUI) onEdit() {
	if len(g.records) == 0 {
		g.statusLabel.SetText("No hay registros para editar")
		return
	}

	if g.selectedRow < 0 || g.selectedRow >= len(g.records) {
		g.statusLabel.SetText("Seleccione un registro de la lista")
		return
	}

	record := g.records[g.selectedRow]
	g.showEditDialog(record)
}

func (g *FacturaGUI) showEditDialog(record map[string]interface{}) {
	if len(g.columns) == 0 {
		return
	}

	entries := make(map[string]*widget.Entry)
	items := make([]*widget.FormItem, 0)

	// Display all fields from the table that we care about
	for _, col := range FacturaFields {
		entry := widget.NewEntry()
		entry.SetText(fmt.Sprintf("%v", record[col]))
		entries[col] = entry
		items = append(items, widget.NewFormItem(col, entry))
	}

	// Create dialog and resize it to be half of main window
	dlg := dialog.NewForm("Editar Factura", "Guardar", "Cancelar", items, func(ok bool) {
		if !ok {
			return
		}

		updates := make(map[string]interface{})
		for col, entry := range entries {
			updates[col] = entry.Text
		}

		idValue := record["CODI"]

		if err := g.module.UpdateRecord("CODI", idValue, updates); err != nil {
			dialog.ShowError(err, g.app.mainWindow)
			return
		}

		g.onSearch()
	}, g.app.mainWindow)

	// Size the dialog to half of main window
	winSize := g.app.mainWindow.Canvas().Size()
	dlg.Resize(fyne.NewSize(winSize.Width/2, winSize.Height/2))
	dlg.Show()
}
