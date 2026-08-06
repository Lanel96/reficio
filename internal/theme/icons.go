package theme

import (
	"fyne.io/fyne/v2"
	"fyne.io/fyne/v2/theme"
)

func IconDatabase() fyne.Resource {
	return theme.FolderIcon()
}

func IconPeople() fyne.Resource {
	return theme.AccountIcon()
}

func IconReceipt() fyne.Resource {
	return theme.FileTextIcon()
}
