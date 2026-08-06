package gui

import (
	"fmt"
	"time"

	"fyne.io/fyne/v2"
	"fyne.io/fyne/v2/container"
	"fyne.io/fyne/v2/dialog"
	"fyne.io/fyne/v2/layout"
	"fyne.io/fyne/v2/widget"

	"reficio/internal/firebird"
	"reficio/internal/modules"
)

type PacienteGUI struct {
	app         *App
	module      *modules.CorrectionModule
	db          *firebird.DBConnection
	codiEntry   *widget.Entry
	nombEntry   *widget.Entry
	resultList  *widget.List
	statusLabel *widget.Label
	countLabel  *widget.Label
	records     []map[string]interface{}
	columns     []string
	selectedRow int
}

// Fields to display in the editor for MPACI table
var PacienteFields = []string{"NOMB", "PATE", "MATE", "NOMBPACI", "FECHNACI"}

func NewPacienteGUI(app *App) *PacienteGUI {
	return &PacienteGUI{
		app:         app,
		selectedRow: -1,
	}
}

func (g *PacienteGUI) Build() fyne.CanvasObject {
	g.statusLabel = widget.NewLabel("Conecte a una BD")
	g.countLabel = widget.NewLabel("0 registros")

	g.codiEntry = widget.NewEntry()
	g.codiEntry.SetPlaceHolder("Ingrese CODI...")

	g.nombEntry = widget.NewEntry()
	g.nombEntry.SetPlaceHolder("Nombre...")

	searchBtn := widget.NewButton("Buscar", g.onSearch)
	searchBtn.Importance = widget.HighImportance

	clearBtn := widget.NewButton("Limpiar", g.onClear)

	searchRow := container.NewHBox(
		widget.NewLabel("CODI:"),
		container.NewGridWrap(fyne.NewSize(150, 36), g.codiEntry),
		widget.NewLabel("Nombre:"),
		container.NewGridWrap(fyne.NewSize(200, 36), g.nombEntry),
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
				nombpac := record["NOMBPACI"]
				pate := record["PATE"]
				label.SetText(fmt.Sprintf("CODI: %v | PAC: %v %v", codi, nombpac, pate))
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

func (g *PacienteGUI) ConnectDB(cfg firebird.Config) error {
	g.db = firebird.NewDBConnection(cfg)
	if err := g.db.TestConnection(); err != nil {
		return err
	}
	g.loadPacienteModule()
	return nil
}

func (g *PacienteGUI) loadPacienteModule() {
	columns, err := g.db.GetColumns("MPACI")
	if err != nil {
		g.statusLabel.SetText("Error al cargar MPACI")
		return
	}

	g.columns = columns
	g.module = modules.NewCorrectionModule(g.db, "MPACI")
	g.module.Columns = columns

	count, err := g.module.GetRecordCount()
	if err == nil {
		g.statusLabel.SetText("MPACI")
		g.countLabel.SetText(fmt.Sprintf("%d registros", count))
	}
}

func (g *PacienteGUI) onSearch() {
	if g.module == nil {
		return
	}

	codi := g.codiEntry.Text
	nomb := g.nombEntry.Text

	// Build search conditions
	// CODI uses exact match, NOMB uses partial match (LIKE)
	searchFields := make(map[string]interface{})
	if codi != "" {
		// Exact match for CODI
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
		return
	}

	if nomb != "" {
		searchFields["NOMB"] = nomb
	}

	if len(searchFields) == 0 {
		return
	}

	result, err := g.module.SearchByMultiple(searchFields)
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

func (g *PacienteGUI) onClear() {
	g.codiEntry.SetText("")
	g.nombEntry.SetText("")
	g.records = nil
	g.selectedRow = -1
	g.resultList.Refresh()
	g.countLabel.SetText("0 registros")
}

func (g *PacienteGUI) onEdit() {
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

func (g *PacienteGUI) showEditDialog(record map[string]interface{}) {
	if len(g.columns) == 0 {
		return
	}

	entries := make(map[string]*widget.Entry)
	items := make([]*widget.FormItem, 0)

	// Display all fields from the table that we care about
	for _, col := range PacienteFields {
		entry := widget.NewEntry()

		// Format date fields properly
		if col == "FECHNACI" {
			if dateVal, ok := record[col].(time.Time); ok {
				entry.SetText(dateVal.Format("2006-01-02"))
			} else {
				entry.SetText(fmt.Sprintf("%v", record[col]))
			}
		} else {
			entry.SetText(fmt.Sprintf("%v", record[col]))
		}

		entries[col] = entry
		items = append(items, widget.NewFormItem(col, entry))
	}

	// Create dialog and resize it to be half of main window
	dlg := dialog.NewForm("Editar Paciente", "Guardar", "Cancelar", items, func(ok bool) {
		if !ok {
			return
		}

		updates := make(map[string]interface{})
		for col, entry := range entries {
			// Handle date field conversion
			if col == "FECHNACI" {
				if dateVal, err := time.Parse("2006-01-02", entry.Text); err == nil {
					updates[col] = dateVal
				} else {
					updates[col] = entry.Text
				}
			} else {
				updates[col] = entry.Text
			}
		}

		idValue := record["CODI"]

		if err := g.module.UpdateRecord("CODI", idValue, updates); err != nil {
			dialog.ShowError(err, g.app.mainWindow)
			return
		}

		// Show success message
		g.statusLabel.SetText("Registro actualizado correctamente")
	}, g.app.mainWindow)

	// Size the dialog to half of main window
	winSize := g.app.mainWindow.Canvas().Size()
	dlg.Resize(fyne.NewSize(winSize.Width/2, winSize.Height/2))
	dlg.Show()
}
