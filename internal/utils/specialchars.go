package utils

import (
	"strings"
)

// SpecialChars contiene mapeo de caracteres especiales comunes
var SpecialChars = map[rune]string{
	// Caracteres acentuados
	'À': "A", 'Á': "A", 'Â': "A", 'Ã': "A", 'Ä': "A", 'Å': "A",
	'à': "a", 'á': "a", 'â': "a", 'ã': "a", 'ä': "a", 'å': "a",
	'È': "E", 'É': "E", 'Ê': "E", 'Ë': "E",
	'è': "e", 'é': "e", 'ê': "e", 'ë': "e",
	'Ì': "I", 'Í': "I", 'Î': "I", 'Ï': "I",
	'ì': "i", 'í': "i", 'î': "i", 'ï': "i",
	'Ò': "O", 'Ó': "O", 'Ô': "O", 'Õ': "O", 'Ö': "O",
	'ò': "o", 'ó': "o", 'ô': "o", 'õ': "o", 'ö': "o",
	'Ù': "U", 'Ú': "U", 'Û': "U", 'Ü': "U",
	'ù': "u", 'ú': "u", 'û': "u", 'ü': "u",
	'Ñ': "N", 'ñ': "n",
	'Ç': "C", 'ç': "c",

	// Símbolos especiales
	'—': "-", '–': "-", '‐': "-",
	'…': "...",
	'«': "\"", '»': "\"",
	'“': "\"", '”': "\"", '„': "\"", '‟': "\"",
	'‘': "'", '’': "'", '‚': "'", '‛': "'",
	'·': "·", '•': "*",
	'©': "(c)", '®': "(r)", '™': "(tm)",
	'€': "EUR", '£': "GBP", '$': "USD",
	'×': "x", '÷': "/",
	'±': "+/-", '∞': "inf", '≠': "!=",
	'≤': "<=", '≥': ">=", '≈': "~=",
	'√': "raiz", 'π': "pi", '∑': "suma",
}

// CleanSpecialChars limpia caracteres especiales y los reemplaza con equivalentes
func CleanSpecialChars(text string) string {
	var result strings.Builder
	for _, r := range text {
		if replacement, ok := SpecialChars[r]; ok {
			result.WriteString(replacement)
		} else {
			result.WriteRune(r)
		}
	}
	return result.String()
}

// FixEncoding corrige problemas de codificación comunes
func FixEncoding(text string) string {
	result := text

	// Corregir dobles codificaciones comunes
	result = strings.ReplaceAll(result, "Ã¡", "á")
	result = strings.ReplaceAll(result, "Ã©", "é")
	result = strings.ReplaceAll(result, "Ã­", "í")
	result = strings.ReplaceAll(result, "Ã³", "ó")
	result = strings.ReplaceAll(result, "Ãº", "ú")
	result = strings.ReplaceAll(result, "Ã±", "ñ")
	result = strings.ReplaceAll(result, "Ã", "A")
	result = strings.ReplaceAll(result, "Ã‰", "É")
	result = strings.ReplaceAll(result, "Ã“", "Ó")
	result = strings.ReplaceAll(result, "Ãš", "Ú")

	// Corregir UTF-8 malformado
	result = strings.ReplaceAll(result, "\x00", "")

	return result
}

// NormalizeText normaliza el texto completamente
func NormalizeText(text string) string {
	result := FixEncoding(text)
	result = CleanSpecialChars(result)
	// Eliminar espacios dobles
	for strings.Contains(result, "  ") {
		result = strings.ReplaceAll(result, "  ", " ")
	}
	// Eliminar espacios al inicio y final de líneas
	lines := strings.Split(result, "\n")
	for i, line := range lines {
		lines[i] = strings.TrimSpace(line)
	}
	return strings.Join(lines, "\n")
}
