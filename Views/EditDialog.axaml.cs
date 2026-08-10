using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace Reficio.Views;

public partial class EditDialog : Window
{
    public Dictionary<string, object?> UpdatedValues { get; private set; } = new();
    private readonly Dictionary<string, TextBox> _fields = new();

    public EditDialog() : this(new Dictionary<string, object?>(), Array.Empty<string>(), "Editar")
    {
    }

    public EditDialog(Dictionary<string, object?> record, string[] fields, string title)
    {
        InitializeComponent();
        Title = title;

        foreach (var field in fields)
        {
            var label = new TextBlock
            {
                Text = GetFieldLabel(field),
                FontWeight = FontWeight.SemiBold,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.Parse("#374151")),
                Margin = new Thickness(0, 10, 0, 4)
            };

            var value = record.TryGetValue(field, out var val) ? val?.ToString() ?? "" : "";
            if (field == "FECHNACI" && DateTime.TryParse(val?.ToString(), out var date))
                value = date.ToString("yyyy-MM-dd");

            var textBox = new TextBox
            {
                Text = value,
                Margin = new Thickness(0, 0, 0, 4),
                Watermark = $"Ingrese {GetFieldLabel(field).ToLower()}"
            };
            _fields[field] = textBox;
            FieldsPanel.Children.Add(label);
            FieldsPanel.Children.Add(textBox);
        }
    }

    private static string GetFieldLabel(string field) => field switch
    {
        "CODI" => "Código",
        "NOMBRECI" => "Nombre",
        "NOMBPACI" => "Nombre completo",
        "USOCFDI" => "Uso CFDI",
        "REGIFISC" => "Registro fiscal",
        "NOMB" => "Nombre",
        "PATE" => "Primer apellido",
        "MATE" => "Segundo apellido",
        "FECHNACI" => "Fecha de nacimiento",
        _ => field
    };

    private void Save_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        UpdatedValues = new Dictionary<string, object?>();
        foreach (var (field, tb) in _fields) UpdatedValues[field] = tb.Text;
        Close(true);
    }

    private void Cancel_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close(false);
}